using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Commands.CancelCustomerOrder;
using Zadana.Application.Modules.Orders.Commands.CreateOrderComplaint;
using Zadana.Application.Modules.Orders.Commands.DeleteCustomerOrder;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class CustomerOrderCommandHandlerTests
{
    [Fact]
    public async Task CancelCustomerOrder_ShouldRejectNonCancellableStage()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id, OrderStatus.ReadyForPickup, "ORD-CANCEL-002");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var publisherMock = new Mock<IPublisher>();
        var handler = new CancelCustomerOrderCommandHandler(dbContext, dbContext, publisherMock.Object);

        var act = () => handler.Handle(
            new CancelCustomerOrderCommand(order.Id, user.Id, "changed_my_mind", null, null),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "ORDER_CANNOT_BE_CANCELLED");
    }

    [Fact]
    public async Task CancelCustomerOrder_ShouldAcceptPredefinedReasonCode()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id, OrderStatus.Preparing, "ORD-CANCEL-003");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var publisherMock = new Mock<IPublisher>();
        var handler = new CancelCustomerOrderCommandHandler(dbContext, dbContext, publisherMock.Object);

        var result = await handler.Handle(
            new CancelCustomerOrderCommand(order.Id, user.Id, "changed_my_mind", null, null),
            CancellationToken.None);

        result.Status.Should().Be("cancelled");
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.StatusHistory.Last().Note.Should().Contain("changed_my_mind");
    }

    [Fact]
    public async Task CancelCustomerOrderValidator_ShouldRequireNoteForOtherReason()
    {
        var validator = new CancelCustomerOrderCommandValidator(CreateLocalizer().Object);

        var result = await validator.ValidateAsync(
            new CancelCustomerOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "other", null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage.Contains("Note is required"));
    }

    [Fact]
    public async Task CancelCustomerOrderValidator_ShouldRejectInvalidReasonCode()
    {
        var validator = new CancelCustomerOrderCommandValidator(CreateLocalizer().Object);

        var result = await validator.ValidateAsync(
            new CancelCustomerOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "not-valid", null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteCustomerOrder_ShouldRemovePendingPaymentOrderAndLinkedPayment()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id, OrderStatus.PendingPayment, "ORD-DELETE-001");
        var payment = new Zadana.Domain.Modules.Payments.Entities.Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        payment.MarkAsPending("Paymob", "provider-delete-1");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var handler = new DeleteCustomerOrderCommandHandler(dbContext, dbContext);

        var result = await handler.Handle(new DeleteCustomerOrderCommand(order.Id, user.Id), CancellationToken.None);

        result.OrderId.Should().Be(order.Id);
        (await dbContext.Orders.AnyAsync(x => x.Id == order.Id)).Should().BeFalse();
        (await dbContext.Payments.AnyAsync(x => x.OrderId == order.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCustomerOrder_ShouldRejectPaidOrder()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id, OrderStatus.PendingPayment, "ORD-DELETE-002");
        order.UpdatePaymentStatus(PaymentStatus.Paid);

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var handler = new DeleteCustomerOrderCommandHandler(dbContext, dbContext);

        var act = () => handler.Handle(new DeleteCustomerOrderCommand(order.Id, user.Id), CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "ORDER_DELETE_NOT_ALLOWED");
    }

    [Fact]
    public async Task CreateOrderComplaint_ShouldPersistComplaintAndAttachments()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var vendor = CreateVendor();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-COMPLAINT-001", vendorId: vendor.Id);

        dbContext.Users.Add(user);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var handler = CreateComplaintHandler(dbContext);

        var result = await handler.Handle(
            new CreateOrderComplaintCommand(
                order.Id,
                user.Id,
                "Order arrived damaged",
                [new CreateOrderComplaintAttachmentItem("photo.jpg", "https://cdn.example.com/photo.jpg")]),
            CancellationToken.None);

        result.Status.Should().Be("submitted");
        dbContext.OrderSupportCases.Should().ContainSingle();
        dbContext.OrderSupportCaseAttachments.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateOrderComplaint_ShouldMergeDuplicateComplaintForSameCustomer()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var vendor = CreateVendor();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-COMPLAINT-002", vendorId: vendor.Id);

        dbContext.Users.Add(user);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(new OrderSupportCase(
            order.Id,
            user.Id,
            OrderSupportCaseType.Complaint,
            OrderSupportCasePriority.Medium,
            OrderSupportCaseQueue.Support,
            null,
            "Existing complaint"));
        await dbContext.SaveChangesAsync();

        var handler = CreateComplaintHandler(dbContext);

        var result = await handler.Handle(
            new CreateOrderComplaintCommand(order.Id, user.Id, "Another complaint", []),
            CancellationToken.None);

        dbContext.OrderSupportCases.Should().ContainSingle();
        dbContext.OrderSupportCases.Single().Activities.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task ApproveReturnRequest_ForCashOnDelivery_ShouldCreateCustomerSpecificCouponAndNoRefund()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var vendor = CreateVendor();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-RETURN-COD-001", PaymentMethodType.CashOnDelivery, vendor.Id);
        var supportCase = CreateReturnRequest(order, user.Id, 85m);

        dbContext.Users.Add(user);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(supportCase);
        dbContext.Payments.Add(new Zadana.Domain.Modules.Payments.Entities.Payment(order.Id, PaymentMethodType.Card, order.TotalAmount));
        await dbContext.SaveChangesAsync();

        var workflowService = CreateWorkflowService(dbContext);

        var result = await workflowService.ApproveAsync(
            supportCase.Id,
            Guid.NewGuid(),
            80m,
            "coupon",
            "platform",
            "Approved as compensation coupon",
            "We issued a coupon for your approved amount.",
            CancellationToken.None);

        result.CompensationType.Should().Be(OrderSupportCaseCompensationType.CouponCompensation);
        result.CompensationCouponId.Should().NotBeNull();
        result.RefundMethod.Should().Be("coupon");

        dbContext.Refunds.Should().BeEmpty();

        var coupon = await dbContext.Coupons.SingleAsync();
        coupon.AssignedUserId.Should().Be(user.Id);
        coupon.SourceType.Should().Be(CouponSourceType.SupportCompensation);
        coupon.OrderSupportCaseId.Should().Be(supportCase.Id);
        coupon.UsageLimit.Should().Be(1);
        coupon.PerUserLimit.Should().Be(1);
        coupon.DiscountValue.Should().Be(80m);
        coupon.EndsAtUtc.Should().NotBeNull();
        coupon.EndsAtUtc.Should().BeAfter(DateTime.UtcNow.AddDays(29));

        order.PaymentStatus.Should().Be(PaymentStatus.PartiallyRefunded);
        order.Status.Should().Be(OrderStatus.Refunded);
    }

    [Fact]
    public async Task ApproveReturnRequest_ForOnlinePayment_ShouldCreateRefundAndNoCoupon()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var vendor = CreateVendor();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-RETURN-ONLINE-001", PaymentMethodType.Card, vendor.Id);
        var supportCase = CreateReturnRequest(order, user.Id, 120m);

        dbContext.Users.Add(user);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(supportCase);
        await dbContext.SaveChangesAsync();

        var workflowService = CreateWorkflowService(dbContext);

        var result = await workflowService.ApproveAsync(
            supportCase.Id,
            Guid.NewGuid(),
            120m,
            "same_method",
            "vendor",
            "Approved as cash refund",
            "Your refund has been approved.",
            CancellationToken.None);

        result.CompensationType.Should().Be(OrderSupportCaseCompensationType.CashRefund);
        result.CompensationCouponId.Should().BeNull();
        result.RefundMethod.Should().Be("same_method");

        dbContext.Coupons.Should().BeEmpty();
        dbContext.Refunds.Should().ContainSingle();

        var refund = await dbContext.Refunds.SingleAsync();
        refund.Amount.Should().Be(120m);
        refund.RefundMethod.Should().Be("same_method");
        refund.OrderSupportCaseId.Should().Be(supportCase.Id);

        order.PaymentStatus.Should().Be(PaymentStatus.PartiallyRefunded);
        order.Status.Should().Be(OrderStatus.Refunded);
    }

    [Fact]
    public async Task ApproveReturnRequest_ShouldRestoreStockForPreviouslyDeductedItems()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var vendor = CreateVendor();
        var masterProductId = Guid.NewGuid();
        var vendorProduct = new Zadana.Domain.Modules.Catalog.Entities.VendorProduct(vendor.Id, masterProductId, 120m, stockQuantity: 2, tradePrice: 90m);
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-RETURN-STOCK-001", PaymentMethodType.Card, vendor.Id, vendorProduct.Id, masterProductId);
        var supportCase = CreateReturnRequest(order, user.Id, 120m);

        order.Items.Single().MarkStockDeducted(DateTime.UtcNow.AddMinutes(-30));
        vendorProduct.DecreaseStock(1);

        dbContext.Users.Add(user);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorProducts.Add(vendorProduct);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(supportCase);
        await dbContext.SaveChangesAsync();

        var workflowService = CreateWorkflowService(dbContext);

        await workflowService.ApproveAsync(
            supportCase.Id,
            Guid.NewGuid(),
            120m,
            "same_method",
            "vendor",
            "Approved as cash refund",
            "Your refund has been approved.",
            CancellationToken.None);

        vendorProduct.StockQuantity.Should().Be(2);
        order.Items.Single().StockRestoredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveReturnRequest_WhenVendorBearsCost_ShouldRecoverFromVendorWallet()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var vendor = CreateVendor();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-RETURN-VENDOR-001", PaymentMethodType.Card, vendor.Id);
        var supportCase = CreateReturnRequest(order, user.Id, 70m);
        var vendorWallet = new Zadana.Domain.Modules.Wallets.Entities.Wallet(
            Zadana.Domain.Modules.Wallets.Enums.WalletOwnerType.Vendor,
            vendor.Id);
        vendorWallet.Credit(100m);

        dbContext.Users.Add(user);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(supportCase);
        dbContext.Wallets.Add(vendorWallet);
        await dbContext.SaveChangesAsync();

        var workflowService = CreateWorkflowService(dbContext);

        var result = await workflowService.ApproveAsync(
            supportCase.Id,
            Guid.NewGuid(),
            70m,
            "same_method",
            "vendor",
            "Approved and charge vendor.",
            "Your refund has been approved.",
            CancellationToken.None);

        result.Status.Should().Be(OrderSupportCaseStatus.Approved);
        var recovery = await dbContext.VendorRecoveries.SingleAsync();
        recovery.TargetAmount.Should().Be(70m);
        recovery.RecoveredAmount.Should().Be(70m);
        recovery.OutstandingAmount.Should().Be(0m);
        recovery.Status.Should().Be(Zadana.Domain.Modules.Wallets.Enums.VendorRecoveryStatus.Recovered);
        vendorWallet.CurrentBalance.Should().Be(30m);
    }

    [Fact]
    public async Task ApproveReturnRequest_WhenVendorWalletIsInsufficient_ShouldLeaveOutstandingRecovery()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var vendor = CreateVendor();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-RETURN-VENDOR-002", PaymentMethodType.Card, vendor.Id);
        var supportCase = CreateReturnRequest(order, user.Id, 90m);
        var vendorWallet = new Zadana.Domain.Modules.Wallets.Entities.Wallet(
            Zadana.Domain.Modules.Wallets.Enums.WalletOwnerType.Vendor,
            vendor.Id);
        vendorWallet.Credit(25m);

        dbContext.Users.Add(user);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(supportCase);
        dbContext.Wallets.Add(vendorWallet);
        await dbContext.SaveChangesAsync();

        var workflowService = CreateWorkflowService(dbContext);

        await workflowService.ApproveAsync(
            supportCase.Id,
            Guid.NewGuid(),
            90m,
            "same_method",
            "vendor",
            "Approved and charge vendor.",
            "Your refund has been approved.",
            CancellationToken.None);

        var recovery = await dbContext.VendorRecoveries.SingleAsync();
        recovery.RecoveredAmount.Should().Be(25m);
        recovery.OutstandingAmount.Should().Be(65m);
        recovery.Status.Should().Be(Zadana.Domain.Modules.Wallets.Enums.VendorRecoveryStatus.PartiallyRecovered);
        vendorWallet.CurrentBalance.Should().Be(0m);
    }

    [Fact]
    public async Task ApproveReturnRequest_ForCashOnDelivery_ShouldRejectSameMethodRefund()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var vendor = CreateVendor();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-RETURN-COD-002", PaymentMethodType.CashOnDelivery, vendor.Id);
        var supportCase = CreateReturnRequest(order, user.Id, 50m);

        dbContext.Users.Add(user);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(supportCase);
        await dbContext.SaveChangesAsync();

        var workflowService = CreateWorkflowService(dbContext);

        var act = () => workflowService.ApproveAsync(
            supportCase.Id,
            Guid.NewGuid(),
            50m,
            "same_method",
            "platform",
            null,
            null,
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "INVALID_RETURN_COMPENSATION_METHOD");
    }

    [Fact]
    public async Task ApproveReturnRequest_ForOnlinePayment_ShouldRejectCouponCompensation()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var vendor = CreateVendor();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-RETURN-ONLINE-002", PaymentMethodType.Card, vendor.Id);
        var supportCase = CreateReturnRequest(order, user.Id, 50m);

        dbContext.Users.Add(user);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(supportCase);
        await dbContext.SaveChangesAsync();

        var workflowService = CreateWorkflowService(dbContext);

        var act = () => workflowService.ApproveAsync(
            supportCase.Id,
            Guid.NewGuid(),
            50m,
            "coupon",
            "platform",
            null,
            null,
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "INVALID_RETURN_COMPENSATION_METHOD");
    }

    private static CreateOrderComplaintCommandHandler CreateComplaintHandler(ApplicationDbContext dbContext)
    {
        return new CreateOrderComplaintCommandHandler(CreateWorkflowService(dbContext));
    }

    private static OrderSupportCaseWorkflowService CreateWorkflowService(ApplicationDbContext dbContext)
    {
        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(x => x.SendToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notificationService
            .Setup(x => x.SendOrderSupportCaseChangedToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pushService = new Mock<IOneSignalPushService>();
        pushService
            .Setup(x => x.SendToExternalUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<OneSignalPushProfile>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(true, false, true, null, null, "test"));

        var vendorPayoutWalletService = new VendorPayoutWalletService(
            dbContext,
            new Mock<Microsoft.Extensions.Logging.ILogger<VendorPayoutWalletService>>().Object);
        var vendorRecoveryService = new VendorRecoveryService(dbContext, vendorPayoutWalletService);

        var workflowService = new OrderSupportCaseWorkflowService(
            dbContext,
            dbContext,
            notificationService.Object,
            pushService.Object,
            null,
            vendorRecoveryService,
            new OrderInventoryWorkflowService(dbContext));

        return workflowService;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateUser() =>
        new("Customer User", "customer.commands@test.com", "01000000001", UserRole.Customer);

    private static Mock<IStringLocalizer<SharedResource>> CreateLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<SharedResource>>();
        localizer
            .Setup(x => x["RequiredField"])
            .Returns(new LocalizedString("RequiredField", "{PropertyName} is required."));
        return localizer;
    }

    private static Order CreateOrder(
        Guid userId,
        OrderStatus status,
        string orderNumber,
        PaymentMethodType paymentMethod = PaymentMethodType.Card,
        Guid? vendorId = null,
        Guid? vendorProductId = null,
        Guid? masterProductId = null)
    {
        var order = new Order(orderNumber, userId, vendorId ?? Guid.NewGuid(), Guid.NewGuid(), paymentMethod, 120m, 0m, 15m, 15m, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m, null, null, false, null, null, null, null, 1, false, 5m);
        order.Items.Add(new OrderItem(order.Id, vendorProductId ?? Guid.NewGuid(), masterProductId ?? Guid.NewGuid(), "Complaint Item", 1, 120m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }

    private static Vendor CreateVendor() =>
        new(
            Guid.NewGuid(),
            "متجر الاختبار",
            "Test Vendor",
            "grocery",
            $"CR-{Guid.NewGuid():N}"[..12],
            "vendor@test.com",
            "01000000009");

    private static OrderSupportCase CreateReturnRequest(Order order, Guid userId, decimal requestedAmount) =>
        new(
            order.Id,
            userId,
            OrderSupportCaseType.ReturnRequest,
            OrderSupportCasePriority.High,
            OrderSupportCaseQueue.Finance,
            "quality_issue",
            "Customer requested a return.",
            DateTime.UtcNow.AddHours(12),
            requestedAmount);
}
