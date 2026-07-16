using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zadana.Api.BackgroundJobs;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.SharedKernel.Primitives;

namespace Zadana.UnitTests.BackgroundJobs;

public class PendingPaymentExpirationWorkerTests
{
    [Fact]
    public async Task RunOnceAsync_WhenCardPaymentReservationExpired_ShouldCancelOrderAndReleaseStock()
    {
        await using var context = CreateContext();
        var setup = await SeedReservedOrderAsync(
            context,
            PaymentMethodType.Card,
            OrderStatus.PendingPayment,
            payment =>
            {
                payment.MarkAsPending("Moyasar", "pay_card_expired");
                MovePaymentIntoThePast(payment);
            });

        var emailCenter = CreateEmailCenter();
        var publisher = new Mock<IPublisher>();
        publisher
            .Setup(service => service.Publish(It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = CreateWorker(context, emailCenter.Object, publisher.Object);

        await worker.RunOnceAsync(CancellationToken.None);

        setup.Order.Status.Should().Be(OrderStatus.Cancelled);
        setup.Order.PaymentStatus.Should().Be(PaymentStatus.Failed);
        setup.Payment.Status.Should().Be(PaymentStatus.Failed);
        setup.VendorProduct.StockQuantity.Should().Be(5);
        setup.OrderItem.StockRestoredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RunOnceAsync_WhenCardOrderHasNewerRetry_ShouldKeepReservation()
    {
        await using var context = CreateContext();
        var setup = await SeedReservedOrderAsync(
            context,
            PaymentMethodType.Card,
            OrderStatus.PendingPayment,
            payment =>
            {
                payment.MarkAsPending("Moyasar", "pay_old_attempt");
                MovePaymentIntoThePast(payment);
            });
        var retryPayment = new Payment(setup.Order.Id, PaymentMethodType.Card, setup.Order.TotalAmount);
        retryPayment.MarkAsPending("Moyasar", "pay_new_attempt");
        Stamp(retryPayment);
        context.Payments.Add(retryPayment);
        await context.SaveChangesAsync();

        var worker = CreateWorker(context);

        await worker.RunOnceAsync(CancellationToken.None);

        setup.Order.Status.Should().Be(OrderStatus.PendingPayment);
        retryPayment.Status.Should().Be(PaymentStatus.Pending);
        setup.VendorProduct.StockQuantity.Should().Be(3);
        setup.OrderItem.StockRestoredAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task RunOnceAsync_WhenBankTransferReservationExpiredWithoutProof_ShouldCancelOrderAndReleaseStock()
    {
        await using var context = CreateContext();
        var setup = await SeedReservedOrderAsync(
            context,
            PaymentMethodType.BankTransfer,
            OrderStatus.PendingBankConfirmation,
            payment =>
            {
                payment.MarkAsPending("BankTransfer", "ZDNEXPIRED");
                payment.ApplyProviderFetch(
                    "awaiting_bank_transfer",
                    "ZDNEXPIRED",
                    JsonSerializer.Serialize(new { expiresAtUtc = DateTime.UtcNow.AddMinutes(-1) }));
                MovePaymentIntoThePast(payment);
            });

        var worker = CreateWorker(context);

        await worker.RunOnceAsync(CancellationToken.None);

        setup.Order.Status.Should().Be(OrderStatus.Cancelled);
        setup.Order.PaymentStatus.Should().Be(PaymentStatus.Failed);
        setup.Payment.Status.Should().Be(PaymentStatus.Failed);
        setup.VendorProduct.StockQuantity.Should().Be(5);
        setup.OrderItem.StockRestoredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RunOnceAsync_WhenBankTransferProofUploaded_ShouldKeepReservation()
    {
        await using var context = CreateContext();
        var setup = await SeedReservedOrderAsync(
            context,
            PaymentMethodType.BankTransfer,
            OrderStatus.PendingBankConfirmation,
            payment =>
            {
                payment.MarkAsPending("BankTransfer", "ZDNPROOF");
                payment.ApplyProviderFetch(
                    "proof_uploaded",
                    "ZDNPROOF",
                    JsonSerializer.Serialize(new { expiresAtUtc = DateTime.UtcNow.AddMinutes(-1) }));
                MovePaymentIntoThePast(payment);
            });

        var worker = CreateWorker(context);

        await worker.RunOnceAsync(CancellationToken.None);

        setup.Order.Status.Should().Be(OrderStatus.PendingBankConfirmation);
        setup.Payment.Status.Should().Be(PaymentStatus.Pending);
        setup.VendorProduct.StockQuantity.Should().Be(3);
        setup.OrderItem.StockRestoredAtUtc.Should().BeNull();
    }

    private static async Task<ReservedOrderSetup> SeedReservedOrderAsync(
        IApplicationDbContext context,
        PaymentMethodType paymentMethod,
        OrderStatus orderStatus,
        Action<Payment>? configurePayment = null)
    {
        var customer = new User("Payment Customer", $"customer-{Guid.NewGuid():N}@test.com", "01010000000", UserRole.Customer);
        var vendorOwner = new User("Payment Vendor", $"vendor-{Guid.NewGuid():N}@test.com", "01020000000", UserRole.Vendor);
        var vendor = new Vendor(vendorOwner.Id, "Payment Vendor Ar", "Payment Vendor", "Retail", $"CR-{Guid.NewGuid():N}"[..16], "vendor@test.com", "01030000000");
        var address = new CustomerAddress(customer.Id, "Payment Customer", "01010000000", "Test address", AddressLabel.Home, city: "Riyadh", area: "Central");
        var order = CreateOrder(customer.Id, vendor.Id, address.Id, paymentMethod);
        if (orderStatus != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(orderStatus, null, "Test status");
        }

        var masterProductId = Guid.NewGuid();
        var vendorProduct = new VendorProduct(vendor.Id, masterProductId, 50m, stockQuantity: 5, tradePrice: 35m);
        var orderItem = new OrderItem(order.Id, vendorProduct.Id, masterProductId, "Reserved Item", 2, 50m, tradeUnitPrice: 35m);
        vendorProduct.DecreaseStock(2);
        orderItem.MarkStockDeducted();
        var payment = new Payment(order.Id, paymentMethod, order.TotalAmount);
        configurePayment?.Invoke(payment);

        Stamp(vendor);
        Stamp(address);
        Stamp(order);
        Stamp(vendorProduct);
        Stamp(payment);

        context.Users.AddRange(customer, vendorOwner);
        context.Vendors.Add(vendor);
        context.CustomerAddresses.Add(address);
        context.VendorProducts.Add(vendorProduct);
        context.Orders.Add(order);
        context.OrderItems.Add(orderItem);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);

        return new ReservedOrderSetup(order, orderItem, vendorProduct, payment);
    }

    private static PendingPaymentExpirationWorker CreateWorker(
        IApplicationDbContext context,
        IEmailCenterService? emailCenterService = null,
        IPublisher? publisher = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton(context);
        services.AddSingleton(new OrderInventoryWorkflowService(context));
        services.AddSingleton(emailCenterService ?? CreateEmailCenter().Object);
        if (publisher is not null)
        {
            services.AddSingleton(publisher);
        }

        var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:CardSessionExpirationSeconds"] = "300",
                ["BankTransfer:ExpirationMinutes"] = "5"
            })
            .Build();

        return new PendingPaymentExpirationWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PendingPaymentExpirationWorker>.Instance,
            configuration);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Mock<IEmailCenterService> CreateEmailCenter()
    {
        var emailCenter = new Mock<IEmailCenterService>();
        emailCenter
            .Setup(service => service.DispatchSystemEventEmailAsync(
                It.IsAny<EmailSystemEventDispatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailDispatchOperationResult(true, true, false, "system_event", "fake", "message-id", null));

        return emailCenter;
    }

    private static void MovePaymentIntoThePast(Payment payment)
    {
        var oldTimestamp = DateTime.UtcNow.AddMinutes(-10);
        payment.CreatedAtUtc = oldTimestamp;
        payment.UpdatedAtUtc = oldTimestamp;
    }

    private static void Stamp(BaseEntity entity)
    {
        if (entity.CreatedAtUtc == default)
        {
            entity.CreatedAtUtc = DateTime.UtcNow;
        }

        if (entity.UpdatedAtUtc == default)
        {
            entity.UpdatedAtUtc = entity.CreatedAtUtc;
        }
    }

    private static Order CreateOrder(Guid userId, Guid vendorId, Guid addressId, PaymentMethodType paymentMethod) =>
        new(
            $"ORD-EXP-{Guid.NewGuid():N}"[..16],
            userId,
            vendorId,
            addressId,
            paymentMethod,
            100m,
            0m,
            10m,
            10m,
            0m,
            0m,
            null,
            null,
            null,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            1,
            false,
            5m);

    private sealed record ReservedOrderSetup(
        Order Order,
        OrderItem OrderItem,
        VendorProduct VendorProduct,
        Payment Payment);
}
