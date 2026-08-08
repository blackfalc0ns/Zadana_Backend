using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Api.Modules.Orders.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Orders;

public class AdminOrdersControllerRefundTests
{
    [Fact]
    public async Task CancelOrder_WithRefund_CreatesReturnCaseAndLinksRefund()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var customer = new User("Refund Customer", "admin.refund.customer@test.com", "01000000001", UserRole.Customer);
        var vendorOwner = new User("Vendor Owner", "admin.refund.vendor@test.com", "01000000002", UserRole.Vendor);
        var vendor = new Vendor(vendorOwner.Id, "Test Vendor Ar", "Test Vendor", "Retail", "CR-ADMIN-REFUND", "vendor@test.com", "01000000003");
        var address = new CustomerAddress(customer.Id, "Refund Customer", "01000000001", "Test address", AddressLabel.Home, city: "Riyadh", area: "Central");
        var order = CreateOrder(customer.Id, vendor.Id, address.Id);
        var payment = new Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        payment.MarkAsPaid();

        dbContext.Users.AddRange(customer, vendorOwner);
        dbContext.Vendors.Add(vendor);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var adminUserId = Guid.NewGuid();
        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(service => service.UserId).Returns(adminUserId);

        var orderReadService = new Mock<IOrderReadService>();
        orderReadService
            .Setup(service => service.GetAdminOrderDetailAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAdminOrderDetail(order.Id, customer.Id));

        var notificationDispatcher = new Mock<IOrderStatusNotificationDispatcher>();
        notificationDispatcher
            .Setup(service => service.DispatchCustomerAsync(
                It.IsAny<OrderStatusCustomerNotificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusNotificationDispatchResult(
                InboxQueued: false,
                RealtimeQueued: false,
                PushAttempted: false,
                PushSent: false,
                PushProviderStatusCode: null,
                PushReason: null));

        var publisher = new Mock<IPublisher>();
        publisher
            .Setup(service => service.Publish(
                It.IsAny<OrderStatusChangedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new AdminOrdersController(
            dbContext,
            currentUserService.Object,
            orderReadService.Object,
            Mock.Of<IOrderSupportCaseWorkflowService>(),
            publisher.Object,
            notificationDispatcher.Object,
            Mock.Of<IDeliveryDispatchService>(),
            Mock.Of<INotificationService>());

        await controller.CancelOrder(
            order.Id,
            new AdminCancelOrderRequest(
                "customer_request",
                "Refund requested by admin cancellation.",
                "partial",
                "Platform",
                NotifyCustomer: true,
                NotifyMerchant: true,
                NotifyDriver: true,
                CustomerMessage: null,
                InternalNote: "Admin cancellation."),
            CancellationToken.None);

        var supportCase = await dbContext.OrderSupportCases
            .Include(item => item.Activities)
            .SingleAsync();
        var refund = await dbContext.Refunds.SingleAsync();
        var savedOrder = await dbContext.Orders.SingleAsync();

        supportCase.OrderId.Should().Be(order.Id);
        supportCase.Type.Should().Be(OrderSupportCaseType.ReturnRequest);
        supportCase.Status.Should().Be(OrderSupportCaseStatus.Approved);
        supportCase.Priority.Should().Be(OrderSupportCasePriority.High);
        supportCase.Queue.Should().Be(OrderSupportCaseQueue.Finance);
        supportCase.ReasonCode.Should().Be("admin_refund");
        supportCase.RequestedRefundAmount.Should().Be(55m);
        supportCase.ApprovedRefundAmount.Should().Be(55m);
        supportCase.CompensationType.Should().Be(OrderSupportCaseCompensationType.CashRefund);
        supportCase.CostBearer.Should().Be("Platform");
        supportCase.Activities.Should().Contain(item => item.Action == "approved");

        refund.OrderSupportCaseId.Should().Be(supportCase.Id);
        refund.Amount.Should().Be(55m);
        refund.RefundMethod.Should().Be("same_method");
        refund.LifecycleStatus.Should().Be(RefundStatus.Succeeded);
        savedOrder.PaymentStatus.Should().Be(PaymentStatus.PartiallyRefunded);
    }

    [Fact]
    public async Task CancelOrder_ShouldReleaseReservedStock()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var customer = new User("Cancel Customer", "admin.cancel.customer@test.com", "01000000011", UserRole.Customer);
        var vendorOwner = new User("Vendor Owner", "admin.cancel.vendor@test.com", "01000000012", UserRole.Vendor);
        var vendor = new Vendor(vendorOwner.Id, "Cancel Vendor Ar", "Cancel Vendor", "Retail", "CR-ADMIN-CANCEL", "cancel.vendor@test.com", "01000000013");
        var address = new CustomerAddress(customer.Id, "Cancel Customer", "01000000011", "Test address", AddressLabel.Home, city: "Riyadh", area: "Central");
        var order = CreateOrder(customer.Id, vendor.Id, address.Id);
        var masterProductId = Guid.NewGuid();
        var vendorProduct = new VendorProduct(vendor.Id, masterProductId, 50m, stockQuantity: 5, tradePrice: 35m);
        var orderItem = new OrderItem(order.Id, vendorProduct.Id, masterProductId, "Reserved Item", 2, 50m, tradeUnitPrice: 35m);
        vendorProduct.DecreaseStock(2);
        orderItem.MarkStockDeducted();

        dbContext.Users.AddRange(customer, vendorOwner);
        dbContext.Vendors.Add(vendor);
        dbContext.CustomerAddresses.Add(address);
        dbContext.VendorProducts.Add(vendorProduct);
        dbContext.Orders.Add(order);
        dbContext.OrderItems.Add(orderItem);
        await dbContext.SaveChangesAsync();

        var adminUserId = Guid.NewGuid();
        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(service => service.UserId).Returns(adminUserId);

        var orderReadService = new Mock<IOrderReadService>();
        orderReadService
            .Setup(service => service.GetAdminOrderDetailAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAdminOrderDetail(order.Id, customer.Id));

        var notificationDispatcher = new Mock<IOrderStatusNotificationDispatcher>();
        notificationDispatcher
            .Setup(service => service.DispatchCustomerAsync(
                It.IsAny<OrderStatusCustomerNotificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusNotificationDispatchResult(
                InboxQueued: false,
                RealtimeQueued: false,
                PushAttempted: false,
                PushSent: false,
                PushProviderStatusCode: null,
                PushReason: null));

        var publisher = new Mock<IPublisher>();
        publisher
            .Setup(service => service.Publish(
                It.IsAny<OrderStatusChangedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new AdminOrdersController(
            dbContext,
            currentUserService.Object,
            orderReadService.Object,
            Mock.Of<IOrderSupportCaseWorkflowService>(),
            publisher.Object,
            notificationDispatcher.Object,
            Mock.Of<IDeliveryDispatchService>(),
            Mock.Of<INotificationService>());

        await controller.CancelOrder(
            order.Id,
            new AdminCancelOrderRequest(
                "customer_request",
                "Cancelled by admin.",
                null,
                null,
                NotifyCustomer: true,
                NotifyMerchant: true,
                NotifyDriver: true,
                CustomerMessage: null,
                InternalNote: "Admin cancellation."),
            CancellationToken.None);

        var savedProduct = await dbContext.VendorProducts.SingleAsync(item => item.Id == vendorProduct.Id);
        var savedOrderItem = await dbContext.OrderItems.SingleAsync(item => item.Id == orderItem.Id);

        savedProduct.StockQuantity.Should().Be(5);
        savedOrderItem.StockRestoredAtUtc.Should().NotBeNull();
    }

    private static Order CreateOrder(Guid userId, Guid vendorId, Guid addressId) =>
        new(
            "ORD-ADMIN-REFUND",
            userId,
            vendorId,
            addressId,
            PaymentMethodType.Card,
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

    private static AdminOrderDetailDto CreateAdminOrderDetail(Guid orderId, Guid customerUserId) =>
        new(
            orderId,
            "ORD-ADMIN-REFUND",
            "Refund Customer",
            "01000000001",
            "admin.refund.customer@test.com",
            "Test address",
            customerUserId,
            "Test merchant",
            "Main branch",
            "Riyadh",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "Riyadh",
            "Central",
            100,
            "2026-07-09",
            "15:06",
            "cancelled",
            "partially_refunded",
            "cancelled",
            "Delivery",
            "not_applicable",
            0,
            null,
            null,
            null,
            [],
            "none",
            string.Empty,
            string.Empty,
            "Card",
            "Today",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            DateTime.UtcNow,
            100m,
            10m,
            new OrderDeliveryBreakdownDto(
                0m,
                0m,
                0m,
                0m,
                10m,
                "test",
                "test",
                "live",
                false,
                "none",
                null,
                null,
                null,
                1,
                false,
                null,
                null),
            0m,
            110m,
            null,
            null,
            null,
            [],
            [],
            [],
            [],
            [],
            null,
            null);
}
