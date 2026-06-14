using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.PlaceOrder;

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext? _context;
    private readonly FinancialSettingsOptions _financialSettings;

    public PlaceOrderCommandHandler(
        IOrderRepository orderRepository,
        IStringLocalizer<SharedResource> localizer,
        IUnitOfWork unitOfWork,
        IApplicationDbContext? context = null,
        IOptions<FinancialSettingsOptions>? financialSettings = null)
    {
        _orderRepository = orderRepository;
        _localizer = localizer;
        _unitOfWork = unitOfWork;
        _context = context;
        _financialSettings = financialSettings?.Value ?? new FinancialSettingsOptions();
    }

    public async Task<Guid> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var cart = await _orderRepository.GetCartForCheckoutAsync(request.UserId, cancellationToken);

        if (cart == null || !cart.Items.Any())
        {
            throw new BusinessRuleException("EMPTY_CART", _localizer["EMPTY_CART"]);
        }

        if (!Enum.TryParse<PaymentMethodType>(request.PaymentMethod, true, out var paymentMethod))
        {
            throw new BusinessRuleException("INVALID_PAYMENT", _localizer["INVALID_PAYMENT"]);
        }

        if (paymentMethod is PaymentMethodType.Wallet or PaymentMethodType.Mada or PaymentMethodType.ApplePay)
        {
            throw new BusinessRuleException(
                "PAYMENT_METHOD_NOT_SUPPORTED",
                $"{paymentMethod} is not supported as a standalone order payment method.");
        }

        var vendorBranchId = await ResolveVendorBranchIdAsync(request, cancellationToken);

        var masterProductIds = cart.Items.Select(item => item.MasterProductId).Distinct().ToArray();
        var vendorProducts = await _orderRepository.GetVendorProductsForCheckoutAsync(
            request.VendorId,
            masterProductIds,
            vendorBranchId,
            cancellationToken);

        var unavailableCartItems = cart.Items
            .Where(item => !vendorProducts.ContainsKey(item.MasterProductId))
            .Select(item => item.ProductName)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (unavailableCartItems.Count > 0)
        {
            throw new BusinessRuleException(
                "CART_ITEMS_UNAVAILABLE_AT_ADDRESS_BRANCH",
                BuildUnavailableCartItemsMessage(unavailableCartItems));
        }

        foreach (var cartItem in cart.Items)
        {
            if (!vendorProducts.TryGetValue(cartItem.MasterProductId, out var vendorProduct))
            {
                throw new BusinessRuleException("VENDOR_MISSING_CART_PRODUCT", _localizer["VENDOR_MISSING_CART_PRODUCT"]);
            }

            if (vendorProduct.StockQuantity < cartItem.Quantity)
            {
                throw new BusinessRuleException("INSUFFICIENT_STOCK", _localizer["INSUFFICIENT_STOCK"]);
            }

            if (!vendorProduct.TradePrice.HasValue)
            {
                throw new BusinessRuleException("INCOMPLETE_VENDOR_PRICING", "Vendor product pricing is incomplete.");
            }
        }

        var subtotal = cart.Items.Sum(item => vendorProducts[item.MasterProductId].SellingPrice * item.Quantity);
        var vendorCommissionRate = vendorProducts.Values.FirstOrDefault()?.Vendor?.CommissionRate ?? 0m;
        var commissionAmount = cart.Items.Sum(item =>
        {
            var vendorProduct = vendorProducts[item.MasterProductId];
            var tradePrice = vendorProduct.TradePrice!.Value;
            var profitPerUnit = Math.Max(vendorProduct.SellingPrice - tradePrice, 0m);
            return Math.Round((profitPerUnit * item.Quantity) * vendorCommissionRate / 100m, 2);
        });
        var itemQuantities = cart.Items
            .GroupBy(item => item.MasterProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var reusableOrder = await _orderRepository.GetReusablePendingOrderForCheckoutAsync(
            request.UserId,
            request.VendorId,
            request.CustomerAddressId,
            paymentMethod,
            vendorBranchId,
            request.CouponId,
            request.Notes,
            subtotal,
            cart.DiscountTotal,
            cart.DeliveryFee,
            request.BaseDeliveryFee,
            request.DistanceDeliveryFee,
            request.SurgeDeliveryFee,
            request.QuotedDistanceKm,
            request.DeliveryPricingMode,
            request.DeliveryPricingRuleLabel,
            request.DriverToVendorDistanceKm,
            request.VendorToCustomerDistanceKm,
            request.DriverToVendorFee,
            request.VendorToCustomerFee,
            request.DriverToVendorPricingSource,
            request.VendorToCustomerPricingSource,
            request.UsedEstimatedDriverPricing,
            request.PricingOriginType,
            request.PricingOriginDriverId,
            request.DeliveryQuoteStatus,
            request.DeliveryQuoteLockedAtUtc,
            request.DeliveryQuoteVersion,
            request.HasDeliveryAnomalyWarning,
            commissionAmount,
            request.VatAmount,
            request.CodFee,
            itemQuantities,
            cancellationToken);

        if (reusableOrder is not null)
        {
            ApplyOrderFinancialSnapshot(reusableOrder, subtotal, cart.DiscountTotal, commissionAmount, request);

            if (request.ClearCartAfterPlacement)
            {
                _orderRepository.RemoveCart(cart);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return reusableOrder.Id;
        }

        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

        var order = new Order(
            orderNumber: orderNumber,
            userId: request.UserId,
            vendorId: request.VendorId,
            customerAddressId: request.CustomerAddressId,
            paymentMethod: paymentMethod,
            subtotal: subtotal,
            discountTotal: cart.DiscountTotal,
            deliveryFee: cart.DeliveryFee,
            baseDeliveryFee: request.BaseDeliveryFee,
            distanceDeliveryFee: request.DistanceDeliveryFee,
            surgeDeliveryFee: request.SurgeDeliveryFee,
            quotedDistanceKm: request.QuotedDistanceKm,
            deliveryPricingMode: request.DeliveryPricingMode,
            deliveryPricingRuleLabel: request.DeliveryPricingRuleLabel,
            driverToVendorDistanceKm: request.DriverToVendorDistanceKm,
            vendorToCustomerDistanceKm: request.VendorToCustomerDistanceKm,
            driverToVendorFee: request.DriverToVendorFee,
            vendorToCustomerFee: request.VendorToCustomerFee,
            driverToVendorPricingSource: request.DriverToVendorPricingSource,
            vendorToCustomerPricingSource: request.VendorToCustomerPricingSource,
            usedEstimatedDriverPricing: request.UsedEstimatedDriverPricing,
            pricingOriginType: request.PricingOriginType,
            pricingOriginDriverId: request.PricingOriginDriverId,
            deliveryQuoteStatus: request.DeliveryQuoteStatus,
            deliveryQuoteLockedAtUtc: request.DeliveryQuoteLockedAtUtc,
            deliveryQuoteVersion: request.DeliveryQuoteVersion,
            hasDeliveryAnomalyWarning: request.HasDeliveryAnomalyWarning,
            commissionAmount: commissionAmount,
            vatAmount: request.VatAmount,
            codFee: request.CodFee,
            notes: request.Notes,
            vendorBranchId: vendorBranchId,
            couponId: request.CouponId
        );
        ApplyOrderFinancialSnapshot(order, subtotal, cart.DiscountTotal, commissionAmount, request);

        _orderRepository.AddOrder(order);

        foreach (var item in cart.Items)
        {
            var vendorProduct = vendorProducts[item.MasterProductId];
            var masterProduct = vendorProduct.MasterProduct;
            var orderItem = new OrderItem(
                orderId: order.Id,
                vendorProductId: vendorProduct.Id,
                masterProductId: item.MasterProductId,
                productName: item.ProductName,
                quantity: item.Quantity,
                unitPrice: vendorProduct.SellingPrice,
                tradeUnitPrice: vendorProduct.TradePrice,
                vendorProfitPerUnit: Math.Max(vendorProduct.SellingPrice - vendorProduct.TradePrice!.Value, 0m)
            );

            // Capture variant snapshot for historical accuracy
            var snapshotImageUrl = masterProduct?.Images
                .OrderByDescending(img => img.IsPrimary)
                .ThenBy(img => img.DisplayOrder)
                .Select(img => img.Url)
                .FirstOrDefault();

            var measurementUnit = masterProduct?.MeasurementUnit ?? masterProduct?.UnitOfMeasure;
            var snapshotIsArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                .Equals("ar", StringComparison.OrdinalIgnoreCase);
            var snapshotDisplaySize = masterProduct is not null
                ? MasterProductDisplayDto.BuildDisplaySize(
                    snapshotIsArabic ? masterProduct.PackageType?.NameAr : masterProduct.PackageType?.NameEn,
                    masterProduct.MeasurementValue,
                    snapshotIsArabic ? measurementUnit?.NameAr : measurementUnit?.NameEn,
                    measurementUnit?.Symbol,
                    snapshotIsArabic)
                : null;

            orderItem.CaptureVariantSnapshot(snapshotImageUrl, snapshotDisplaySize, masterProduct?.Barcode);

            _orderRepository.AddOrderItem(orderItem);
        }

        if (request.ClearCartAfterPlacement)
        {
            _orderRepository.RemoveCart(cart);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.Id;
    }

    private async Task<Guid?> ResolveVendorBranchIdAsync(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        if (_context is null)
        {
            return request.VendorBranchId;
        }

        var customerAddress = await _context.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(address => address.Id == request.CustomerAddressId && address.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("CustomerAddress", request.CustomerAddressId);

        var selectedBranchIds = await CartBranchSelectionSupport.ResolveAddressBranchIdsByVendorAsync(
            _context,
            [request.VendorId],
            customerAddress,
            cancellationToken);

        if (!selectedBranchIds.TryGetValue(request.VendorId, out var resolvedBranchId))
        {
            return null;
        }

        if (!resolvedBranchId.HasValue && !string.IsNullOrWhiteSpace(customerAddress.City))
        {
            throw new BusinessRuleException(
                "CART_ITEMS_UNAVAILABLE_AT_ADDRESS_BRANCH",
                BuildUnavailableCartItemsMessage([]));
        }

        return resolvedBranchId;
    }

    private static string BuildUnavailableCartItemsMessage(IReadOnlyCollection<string> productNames)
    {
        var names = string.Join(", ", productNames.Where(name => !string.IsNullOrWhiteSpace(name)).Take(5));
        if (string.IsNullOrWhiteSpace(names))
        {
            return IsArabic()
                ? "بعض المنتجات في العربة غير متوفرة في فرع المتجر المطابق لعنوانك."
                : "Some cart items are unavailable at the store branch matching your address.";
        }

        return IsArabic()
            ? $"المنتجات التالية غير متوفرة في فرع المتجر المطابق لعنوانك: {names}"
            : $"The following products are unavailable at the store branch matching your address: {names}";
    }

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private void ApplyOrderFinancialSnapshot(
        Order order,
        decimal subtotal,
        decimal discountTotal,
        decimal vendorCommissionAmount,
        PlaceOrderCommand request)
    {
        var productNet = Math.Max(0m, subtotal - discountTotal);
        var driverCommissionAmount = decimal.Round(
            Math.Max(order.DeliveryFee, 0m) * _financialSettings.DriverCommissionRatePercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        var taxPolicySnapshot = JsonSerializer.Serialize(new
        {
            vat_amount = request.VatAmount,
            cod_fee = request.CodFee,
            cod_fee_applied = request.CodFee > 0m,
        });

        var commissionPolicySnapshot = JsonSerializer.Serialize(new
        {
            vendor_commission_amount = vendorCommissionAmount,
            driver_commission_rate_percent = _financialSettings.DriverCommissionRatePercent,
            driver_commission_amount = driverCommissionAmount,
        });

        order.ApplyFinancialSnapshot(
            productGross: subtotal,
            productNet: productNet,
            vendorCommissionAmount: vendorCommissionAmount,
            driverCommissionAmount: driverCommissionAmount,
            currency: "SAR",
            pricingMode: request.DeliveryPricingMode ?? "live",
            taxPolicySnapshot: taxPolicySnapshot,
            commissionPolicySnapshot: commissionPolicySnapshot);
    }
}
