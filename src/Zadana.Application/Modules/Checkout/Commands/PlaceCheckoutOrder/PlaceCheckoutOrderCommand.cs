using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Payments.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Checkout.Commands.PlaceCheckoutOrder;

public record PlaceCheckoutOrderCommand(
    Guid UserId,
    Guid? VendorId,
    Guid? AddressId,
    string? DeliverySlotId,
    string PaymentMethod,
    string? PromoCode,
    string? Notes,
    string? DeviceId = null,
    bool RemoveUnavailableItems = false) : IRequest<PlaceCheckoutOrderResultDto>;

public class PlaceCheckoutOrderCommandValidator : AbstractValidator<PlaceCheckoutOrderCommand>
{
    public PlaceCheckoutOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PaymentMethod).NotEmpty();
        RuleFor(x => x.PromoCode).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class PlaceCheckoutOrderCommandHandler : IRequestHandler<PlaceCheckoutOrderCommand, PlaceCheckoutOrderResultDto>
{
    private const string CardProvider = "Moyasar";
    private sealed record BankTransferAccount(
        string ProviderName,
        string BankName,
        string AccountHolderName,
        string Iban,
        string? AccountNumber,
        string CountryCode,
        string City,
        int ExpirationMinutes);

    private readonly IApplicationDbContext _context;
    private readonly IPaymentGatewayResolver _gatewayResolver;
    private readonly IDeliveryPricingService _deliveryPricingService;
    private readonly ISender _sender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly BankTransferSettingsOptions _bankTransferSettings;

    public PlaceCheckoutOrderCommandHandler(
        IApplicationDbContext context,
        IPaymentGatewayResolver gatewayResolver,
        IDeliveryPricingService deliveryPricingService,
        ISender sender,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IOptions<BankTransferSettingsOptions>? bankTransferSettings = null)
    {
        _context = context;
        _gatewayResolver = gatewayResolver;
        _deliveryPricingService = deliveryPricingService;
        _sender = sender;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _bankTransferSettings = bankTransferSettings?.Value ?? new BankTransferSettingsOptions();
    }

    public async Task<PlaceCheckoutOrderResultDto> Handle(PlaceCheckoutOrderCommand request, CancellationToken cancellationToken)
    {
        ValidateDeliverySlot(request.DeliverySlotId);

        var paymentMethodCode = CheckoutSupport.NormalizePaymentMethodCode(request.PaymentMethod)
            ?? throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Selected payment method is not supported.");

        var cart = await CheckoutSupport.GetRequiredCartAsync(_context, request.UserId, cancellationToken, asTracking: true);
        var address = await CheckoutSupport.ResolveSelectedAddressAsync(_context, request.UserId, request.AddressId, cancellationToken);
        if (address is null)
        {
            if (request.AddressId.HasValue)
            {
                throw new NotFoundException("CustomerAddress", request.AddressId.Value);
            }

            throw new BusinessRuleException("CUSTOMER_ADDRESS_REQUIRED", "Customer must have a default address before placing an order.");
        }

        var pricing = await CheckoutSupport.BuildPricingSnapshotAsync(_context, cart, request.VendorId, address, cancellationToken);
        if (pricing.UnavailableItems.Count > 0)
        {
            if (!request.RemoveUnavailableItems)
            {
                throw new BusinessRuleException(
                    "CART_UNAVAILABLE_ITEMS_CONFIRMATION_REQUIRED",
                    "Some cart items are unavailable for the selected vendor. Confirm removing unavailable items to continue checkout.");
            }

            RemoveUnavailableCartItems(cart, pricing.UnavailableItems.Select(item => item.Id));
        }

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var deliveryBranchId = await CheckoutSupport.ResolveDeliveryBranchIdAsync(_context, pricing, address, cancellationToken);
        var deliveryAssessment = await CheckoutSupport.EvaluateDeliveryAsync(
            _context,
            _deliveryPricingService,
            deliveryBranchId,
            address,
            cancellationToken,
            pricing.Subtotal);
        if (!deliveryAssessment.DeliveryCheck.CanProceedToCheckout)
        {
            throw deliveryAssessment.DeliveryCheck.Status switch
            {
                "address_required" => new BusinessRuleException("CUSTOMER_ADDRESS_REQUIRED", deliveryAssessment.DeliveryCheck.MessageEn),
                "undeliverable" => new BusinessRuleException("DELIVERY_NOT_AVAILABLE", deliveryAssessment.DeliveryCheck.MessageEn),
                _ => new BusinessRuleException("DELIVERY_PRICING_UNAVAILABLE", deliveryAssessment.DeliveryCheck.MessageEn)
            };
        }

        var deliveryQuote = deliveryAssessment.DeliveryQuote;
        var preparationTimeMinutes = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == pricing.VendorId)
            .Select(v => v.PreparationTimeMinutes)
            .FirstOrDefaultAsync(cancellationToken);
        var operationalProfile = await DeliveryEtaTelemetry.LoadOperationalProfileAsync(
            _context,
            pricing.VendorId,
            deliveryBranchId,
            address.City,
            address.Area,
            cancellationToken);
        var liveSignal = await DeliveryEtaTelemetry.LoadLiveSignalAsync(
            _context,
            deliveryBranchId,
            cancellationToken);
        var estimatedDeliveryWindow = DeliveryEtaPolicy.EstimateCheckoutWindow(
            preparationTimeMinutes,
            deliveryQuote.DriverToVendorDistanceKm,
            deliveryQuote.VendorToCustomerDistanceKm,
            operationalProfile,
            liveSignal);
        var coupon = await ResolveOrderCouponAsync(request.UserId, cart, request.PromoCode, pricing.VendorId, pricing.Subtotal, cancellationToken);
        var discount = coupon == null ? 0m : CheckoutSupport.CalculateDiscountAmount(coupon, pricing.Subtotal);
        var financeBreakdown = await CheckoutSupport.ResolveFinanceBreakdownV2Async(
            _context,
            address,
            pricing.Subtotal,
            deliveryQuote.TotalFee,
            discount,
            paymentMethodCode,
            cancellationToken);

        cart.UpdateTotals(
            pricing.Subtotal,
            deliveryQuote.TotalFee,
            deliveryQuote.BaseFee,
            deliveryQuote.DistanceFee,
            deliveryQuote.SurgeFee,
            deliveryQuote.DistanceKm,
            deliveryQuote.PricingMode,
            deliveryQuote.RuleLabel,
            deliveryQuote.DriverToVendorDistanceKm,
            deliveryQuote.VendorToCustomerDistanceKm,
            deliveryQuote.DriverToVendorFee,
            deliveryQuote.VendorToCustomerFee,
            deliveryQuote.DriverToVendorPricingSource,
            deliveryQuote.VendorToCustomerPricingSource,
            deliveryQuote.UsedEstimatedDriverPricing,
            deliveryQuote.PricingOriginType,
            deliveryQuote.PricingOriginDriverId,
            deliveryQuote.DeliveryQuoteStatus,
            deliveryQuote.QuoteLockedAtUtc,
            deliveryQuote.QuoteVersion,
            deliveryQuote.HasAnomalyWarning);
        if (coupon == null)
        {
            cart.RemoveCoupon();
        }
        else
        {
            cart.ApplyCoupon(coupon.Id, discount);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var internalPaymentMethod = CheckoutSupport.MapPaymentMethodCodeToEnumName(paymentMethodCode);
        var shouldClearCartAfterPlacement = paymentMethodCode is "cash" or "bank";
        var orderId = await _sender.Send(
            new PlaceOrderCommand(
                UserId: request.UserId,
                VendorId: pricing.VendorId,
                CustomerAddressId: address.Id,
                PaymentMethod: internalPaymentMethod,
                Notes: request.Notes,
                VendorBranchId: deliveryBranchId,
                CouponId: coupon?.Id,
                BaseDeliveryFee: deliveryQuote.BaseFee,
                DistanceDeliveryFee: deliveryQuote.DistanceFee,
                SurgeDeliveryFee: deliveryQuote.SurgeFee,
                QuotedDistanceKm: deliveryQuote.DistanceKm,
                DeliveryPricingMode: deliveryQuote.PricingMode,
                DeliveryPricingRuleLabel: deliveryQuote.RuleLabel,
                DriverToVendorDistanceKm: deliveryQuote.DriverToVendorDistanceKm,
                VendorToCustomerDistanceKm: deliveryQuote.VendorToCustomerDistanceKm,
                DriverToVendorFee: deliveryQuote.DriverToVendorFee,
                VendorToCustomerFee: deliveryQuote.VendorToCustomerFee,
                DriverToVendorPricingSource: deliveryQuote.DriverToVendorPricingSource,
                VendorToCustomerPricingSource: deliveryQuote.VendorToCustomerPricingSource,
                UsedEstimatedDriverPricing: deliveryQuote.UsedEstimatedDriverPricing,
                PricingOriginType: deliveryQuote.PricingOriginType,
                PricingOriginDriverId: deliveryQuote.PricingOriginDriverId,
                DeliveryQuoteStatus: deliveryQuote.DeliveryQuoteStatus,
                DeliveryQuoteLockedAtUtc: deliveryQuote.QuoteLockedAtUtc,
                DeliveryQuoteVersion: deliveryQuote.QuoteVersion,
                HasDeliveryAnomalyWarning: deliveryQuote.HasAnomalyWarning,
                VatAmount: financeBreakdown.VatAmount,
                CodFee: financeBreakdown.CodFee,
                ClearCartAfterPlacement: shouldClearCartAfterPlacement),
            cancellationToken);

        var order = await _context.Orders
            .AsTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);
        order.CaptureEtaSnapshot(
            estimatedDeliveryWindow.MinMinutes,
            estimatedDeliveryWindow.MaxMinutes,
            estimatedDeliveryWindow.Confidence,
            estimatedDeliveryWindow.Source,
            estimatedDeliveryWindow.IsApproximate,
            estimatedDeliveryWindow.CalculationMode,
            estimatedDeliveryWindow.Explanation,
            DateTime.UtcNow);

        var payment = new Payment(order.Id, Enum.Parse<PaymentMethodType>(internalPaymentMethod, true), order.TotalAmount);
        payment.SetCheckoutDeviceId(request.DeviceId);
        _context.Payments.Add(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        CheckoutPaymentSessionDto? paymentSession = null;

        if (paymentMethodCode is "card" or "apple_pay")
        {
            if (!_gatewayResolver.TryResolve(CardProvider, out var gateway) || gateway is null)
            {
                throw new BusinessRuleException("PAYMENT_UNAVAILABLE", "Card checkout provider is disabled or not configured.");
            }

            try
            {
                CurrencyPolicy.EnsureOfficial(order.Currency);
                var idempotencyKey = $"payment-create:{order.Id:N}:{payment.Id:N}";
                var channel = paymentMethodCode == "apple_pay"
                    ? PaymentMethodChannel.ApplePay
                    : PaymentMethodChannel.Card;
                var providerMethod = paymentMethodCode == "apple_pay" ? "applepay" : "creditcard";
                var session = await gateway.CreateSessionAsync(
                    new CreatePaymentSessionCommand(
                        OrderId: order.Id,
                        PaymentId: payment.Id,
                        Channel: channel,
                        Amount: order.TotalAmount,
                        Currency: order.Currency,
                        Description: $"Order {order.OrderNumber}",
                        CallbackUrl: string.Empty,
                        IdempotencyKey: idempotencyKey,
                        Metadata: new Dictionary<string, string>
                        {
                            ["order_id"] = order.Id.ToString(),
                            ["payment_id"] = payment.Id.ToString(),
                            ["order_number"] = order.OrderNumber,
                        },
                        CustomerEmail: user.Email,
                        CustomerPhone: user.PhoneNumber,
                        CustomerFullName: user.FullName),
                    cancellationToken);

                payment.ApplyProviderSession(
                    providerName: session.ProviderName,
                    providerMethod: providerMethod,
                    providerPaymentId: session.ProviderPaymentId,
                    providerInvoiceId: session.ProviderInvoiceId,
                    idempotencyKey: idempotencyKey,
                    rawCreateResponse: session.RawCreateResponse,
                    currency: order.Currency);

                paymentSession = new CheckoutPaymentSessionDto(
                    payment.Id,
                    session.ProviderName.ToLowerInvariant(),
                    CheckoutSupport.MapPaymentStatusToContractValue(payment.Status.ToString()),
                    session.ClientAction,
                    session.ProviderPaymentId ?? string.Empty,
                    session.ProviderConfig,
                    PaymentFlow: "online_gateway",
                    IsPaid: false,
                    RequiresCustomerAction: true,
                    CustomerAction: "render_payment_form",
                    ConfirmationMode: "provider_payment_id");
            }
            catch
            {
                await UnconfirmedCardPaymentCleanup.DeleteOrderAsync(_context, order.Id, cancellationToken);
                throw;
            }
        }
        else if (paymentMethodCode == "cash")
        {
            payment.MarkAsPending("CashOnDelivery", $"COD-{order.OrderNumber}");
            order.ChangeStatus(OrderStatus.Placed, null, "Cash on delivery selected");
            order.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "Awaiting vendor response");
            OrderStatusHistoryTracking.TrackNewEntries(_context, order);
        }
        else if (paymentMethodCode == "bank")
        {
            var bankTransferAccount = await ResolveBankTransferAccountAsync(cancellationToken);

            var bankReference = CreateBankTransferReference(order.OrderNumber, payment.Id);
            var expiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(bankTransferAccount.ExpirationMinutes, 5));
            payment.MarkAsPending(bankTransferAccount.ProviderName, bankReference);
            payment.ApplyProviderFetch(
                providerStatus: "awaiting_bank_transfer",
                providerReferenceNumber: bankReference,
                rawFetchResponse: System.Text.Json.JsonSerializer.Serialize(new
                {
                    bankName = bankTransferAccount.BankName,
                    accountHolderName = bankTransferAccount.AccountHolderName,
                    iban = bankTransferAccount.Iban,
                    accountNumber = bankTransferAccount.AccountNumber,
                    countryCode = bankTransferAccount.CountryCode,
                    city = bankTransferAccount.City,
                    reference = bankReference,
                    amount = order.TotalAmount,
                    currency = order.Currency,
                    expiresAtUtc,
                }));

            order.ChangeStatus(OrderStatus.PendingBankConfirmation, null, "Awaiting automatic bank transfer confirmation");
            OrderStatusHistoryTracking.TrackNewEntries(_context, order);

            paymentSession = new CheckoutPaymentSessionDto(
                payment.Id,
                bankTransferAccount.ProviderName.ToLowerInvariant(),
                CheckoutSupport.MapPaymentStatusToContractValue(payment.Status.ToString()),
                "ShowBankTransferInstructions",
                bankReference,
                new
                {
                    bankName = bankTransferAccount.BankName,
                    accountHolderName = bankTransferAccount.AccountHolderName,
                    iban = bankTransferAccount.Iban,
                    accountNumber = bankTransferAccount.AccountNumber,
                    countryCode = bankTransferAccount.CountryCode,
                    city = bankTransferAccount.City,
                    reference = bankReference,
                    amount = order.TotalAmount,
                    currency = order.Currency,
                    expiresAtUtc,
                    webhookDriven = true
                },
                PaymentFlow: "manual_bank_transfer",
                IsPaid: false,
                RequiresCustomerAction: true,
                CustomerAction: "show_bank_transfer_instructions",
                ConfirmationMode: "bank_transfer_webhook");
        }
        else
        {
            throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Selected payment method is not supported.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish notification for order placement or bank-transfer waiting state.
        if (paymentMethodCode is "cash" or "bank")
        {
            await _publisher.Publish(
                new OrderStatusChangedNotification(
                    order.Id,
                    request.UserId,
                    order.VendorId,
                    order.OrderNumber,
                    OrderStatus.PendingPayment,
                    order.Status,
                    NotifyCustomer: true,
                    NotifyVendor: paymentMethodCode is "cash",
                    ActorRole: "customer"),
                cancellationToken);
        }

        return new PlaceCheckoutOrderResultDto(
            LocalizedMessages.GetAr(LocalizedMessages.OrderPlacedSuccess),
            LocalizedMessages.GetEn(LocalizedMessages.OrderPlacedSuccess),
            new CheckoutPlacedOrderDto(
                order.Id,
                order.PlacedAtUtc,
                CheckoutSupport.MapOrderStatusToContractValue(order.Status.ToString()),
                paymentMethodCode,
                CheckoutSupport.MapPaymentStatusToContractValue(order.PaymentStatus.ToString()),
                order.TotalAmount),
            paymentSession);
    }

    private async Task<Zadana.Domain.Modules.Marketing.Entities.Coupon?> ResolveOrderCouponAsync(
        Guid userId,
        Zadana.Domain.Modules.Orders.Entities.Cart cart,
        string? promoCode,
        Guid vendorId,
        decimal subtotal,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            return await CheckoutSupport.ResolveCouponByCodeAsync(_context, userId, promoCode, vendorId, subtotal, cancellationToken);
        }

        return await CheckoutSupport.ResolveAppliedCouponAsync(_context, userId, cart, cancellationToken);
    }

    private static void ValidateDeliverySlot(string? deliverySlotId)
    {
        if (!string.IsNullOrWhiteSpace(deliverySlotId) &&
            !deliverySlotId.Trim().Equals(CheckoutSupport.DefaultDeliverySlotId, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("DELIVERY_SLOT_NOT_AVAILABLE", "Selected delivery slot is not available.");
        }
    }

    private static void RemoveUnavailableCartItems(
        Zadana.Domain.Modules.Orders.Entities.Cart cart,
        IEnumerable<Guid> unavailableCartItemIds)
    {
        var ids = unavailableCartItemIds.ToHashSet();
        if (ids.Count == 0)
        {
            return;
        }

        foreach (var cartItem in cart.Items.Where(item => ids.Contains(item.Id)).ToList())
        {
            cart.Items.Remove(cartItem);
        }
    }

    private async Task<BankTransferAccount> ResolveBankTransferAccountAsync(CancellationToken cancellationToken)
    {
        var platformAccount = await _context.PlatformBankAccounts
            .AsNoTracking()
            .Where(account => account.IsActive)
            .OrderByDescending(account => account.UpdatedAtUtc)
            .ThenByDescending(account => account.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (platformAccount is not null)
        {
            if (!platformAccount.IsBankTransferEnabled ||
                (string.IsNullOrWhiteSpace(platformAccount.IBAN) &&
                 string.IsNullOrWhiteSpace(platformAccount.AccountNumber)))
            {
                throw new BusinessRuleException(
                    "BANK_TRANSFER_UNAVAILABLE",
                    "Platform bank transfer account is not configured.");
            }

            return new BankTransferAccount(
                _bankTransferSettings.ProviderName,
                platformAccount.BankName,
                platformAccount.AccountHolderName,
                platformAccount.IBAN,
                platformAccount.AccountNumber,
                platformAccount.CountryCode,
                platformAccount.City,
                _bankTransferSettings.ExpirationMinutes);
        }

        if (!_bankTransferSettings.Enabled ||
            (string.IsNullOrWhiteSpace(_bankTransferSettings.Iban) &&
             string.IsNullOrWhiteSpace(_bankTransferSettings.AccountNumber)))
        {
            throw new BusinessRuleException(
                "BANK_TRANSFER_UNAVAILABLE",
                "Bank transfer payment is not configured.");
        }

        return new BankTransferAccount(
            _bankTransferSettings.ProviderName,
            _bankTransferSettings.BankName,
            _bankTransferSettings.AccountHolderName,
            _bankTransferSettings.Iban,
            _bankTransferSettings.AccountNumber,
            "SA",
            "Riyadh",
            _bankTransferSettings.ExpirationMinutes);
    }

    private static string CreateBankTransferReference(string orderNumber, Guid paymentId)
    {
        var cleanOrderNumber = new string(orderNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cleanOrderNumber.Length > 12)
        {
            cleanOrderNumber = cleanOrderNumber[^12..];
        }

        return $"ZDN{cleanOrderNumber}{paymentId.ToString("N")[..8]}";
    }
}
