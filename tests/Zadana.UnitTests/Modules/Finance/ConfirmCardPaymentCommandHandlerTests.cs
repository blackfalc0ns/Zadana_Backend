using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Payments.Commands.ConfirmCardPayment;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public class ConfirmCardPaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenProviderTransactionIsNotStoredYet_ResolvesPaymentFromProviderMetadata()
    {
        await using var context = TestDbContextFactory.Create();
        var (order, payment) = SeedPendingCardPayment(context, 130.75m);
        await context.SaveChangesAsync();

        var providerPaymentId = "pay_test_metadata_1";
        var gateway = new StaticPaymentGateway(new GatewayPaymentDetails(
            ProviderName: "Moyasar",
            ProviderPaymentId: providerPaymentId,
            ProviderStatus: "paid",
            AmountMinorUnits: 13075,
            Currency: "SAR",
            Metadata: new Dictionary<string, string>
            {
                ["payment_id"] = payment.Id.ToString(),
                ["order_id"] = order.Id.ToString()
            },
            ProviderReferenceNumber: "rrn-123",
            RawResponse: "{}"));

        var handler = CreateHandler(context, gateway);

        var result = await handler.Handle(
            new ConfirmCardPaymentCommand(
                PaymentId: null,
                ProviderPaymentId: providerPaymentId,
                ProviderName: "Moyasar"),
            CancellationToken.None);

        result.PaymentId.Should().Be(payment.Id);
        result.PaymentStatus.Should().Be("paid");
        result.OrderStatus.Should().Be("pending_vendor_acceptance");

        var savedPayment = await context.Payments.Include(x => x.Order).SingleAsync();
        savedPayment.ProviderTransactionId.Should().Be(providerPaymentId);
        savedPayment.Status.Should().Be(PaymentStatus.Paid);
        savedPayment.Order.Status.Should().Be(OrderStatus.PendingVendorAcceptance);
        gateway.FetchedPaymentIds.Should().Equal(providerPaymentId);
    }

    private static ConfirmCardPaymentCommandHandler CreateHandler(
        Zadana.Infrastructure.Persistence.ApplicationDbContext context,
        StaticPaymentGateway gateway)
    {
        var posting = new FinancialEventPostingService(context, NullLogger<FinancialEventPostingService>.Instance);
        var projection = new WalletProjectionUpdater(context);
        var captureService = new OnlinePaymentCaptureService(
            context,
            posting,
            projection,
            NullLogger<OnlinePaymentCaptureService>.Instance);

        return new ConfirmCardPaymentCommandHandler(
            context,
            new StaticPaymentGatewayResolver(gateway),
            context,
            new NoOpPublisher(),
            captureService,
            Mock.Of<IEmailCenterService>(),
            NullLogger<ConfirmCardPaymentCommandHandler>.Instance);
    }

    private static (Order Order, Payment Payment) SeedPendingCardPayment(
        Zadana.Infrastructure.Persistence.ApplicationDbContext context,
        decimal totalAmount)
    {
        var order = new Order(
            orderNumber: "ORD-CONFIRM-1",
            userId: Guid.NewGuid(),
            vendorId: Guid.NewGuid(),
            customerAddressId: Guid.NewGuid(),
            paymentMethod: PaymentMethodType.Card,
            subtotal: totalAmount,
            discountTotal: 0m,
            deliveryFee: 0m,
            baseDeliveryFee: 0m,
            distanceDeliveryFee: 0m,
            surgeDeliveryFee: 0m,
            quotedDistanceKm: null,
            deliveryPricingMode: "live",
            deliveryPricingRuleLabel: null,
            driverToVendorDistanceKm: 0m,
            vendorToCustomerDistanceKm: 0m,
            driverToVendorFee: 0m,
            vendorToCustomerFee: 0m,
            driverToVendorPricingSource: null,
            vendorToCustomerPricingSource: null,
            usedEstimatedDriverPricing: false,
            pricingOriginType: null,
            pricingOriginDriverId: null,
            deliveryQuoteStatus: null,
            deliveryQuoteLockedAtUtc: null,
            deliveryQuoteVersion: 1,
            hasDeliveryAnomalyWarning: false,
            commissionAmount: 0m);

        order.ApplyFinancialSnapshot(
            productGross: totalAmount,
            productNet: totalAmount,
            vendorCommissionAmount: 0m,
            driverCommissionAmount: 0m,
            currency: "SAR",
            pricingMode: "live",
            taxPolicySnapshot: null,
            commissionPolicySnapshot: null);

        var payment = new Payment(order.Id, PaymentMethodType.Card, totalAmount);
        payment.ApplyProviderSession(
            providerName: "Moyasar",
            providerMethod: "creditcard",
            providerPaymentId: null,
            providerInvoiceId: null,
            idempotencyKey: $"confirm-card:{order.Id}",
            rawCreateResponse: "{}",
            currency: "SAR");

        context.Orders.Add(order);
        context.Payments.Add(payment);
        return (order, payment);
    }

    private sealed class StaticPaymentGatewayResolver : IPaymentGatewayResolver
    {
        private readonly StaticPaymentGateway _gateway;

        public StaticPaymentGatewayResolver(StaticPaymentGateway gateway)
        {
            _gateway = gateway;
        }

        public IPaymentGateway Resolve(string providerName)
        {
            if (!TryResolve(providerName, out var gateway) || gateway is null)
            {
                throw new InvalidOperationException($"No gateway registered for {providerName}.");
            }

            return gateway;
        }

        public bool TryResolve(string providerName, out IPaymentGateway? gateway)
        {
            gateway = string.Equals(providerName, _gateway.ProviderName, StringComparison.OrdinalIgnoreCase)
                ? _gateway
                : null;
            return gateway is not null;
        }

        public IReadOnlyList<IPaymentGateway> GetEnabledGateways() => [_gateway];
    }

    private sealed class StaticPaymentGateway : IPaymentGateway
    {
        private readonly GatewayPaymentDetails _details;

        public StaticPaymentGateway(GatewayPaymentDetails details)
        {
            _details = details;
        }

        public string ProviderName => _details.ProviderName;
        public bool IsEnabled => true;
        public List<string> FetchedPaymentIds { get; } = [];

        public Task<CreatePaymentSessionResult> CreateSessionAsync(
            CreatePaymentSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GatewayPaymentDetails> FetchPaymentAsync(string providerPaymentId, CancellationToken cancellationToken)
        {
            FetchedPaymentIds.Add(providerPaymentId);
            return Task.FromResult(_details);
        }

        public Task<RefundGatewayResult> RefundAsync(RefundGatewayCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            Task.CompletedTask;
    }
}
