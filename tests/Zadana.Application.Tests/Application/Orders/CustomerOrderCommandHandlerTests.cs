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
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
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
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-COMPLAINT-001");

        dbContext.Users.Add(user);
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
    public async Task CreateOrderComplaint_ShouldRejectDuplicateComplaint()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-COMPLAINT-002");

        dbContext.Users.Add(user);
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

        var act = () => handler.Handle(
            new CreateOrderComplaintCommand(order.Id, user.Id, "Another complaint", []),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "ORDER_SUPPORT_CASE_ALREADY_EXISTS");
    }

    private static CreateOrderComplaintCommandHandler CreateComplaintHandler(ApplicationDbContext dbContext)
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

        var workflowService = new OrderSupportCaseWorkflowService(
            dbContext,
            dbContext,
            notificationService.Object,
            pushService.Object);

        return new CreateOrderComplaintCommandHandler(workflowService);
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

    private static Order CreateOrder(Guid userId, OrderStatus status, string orderNumber)
    {
        var order = new Order(orderNumber, userId, Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.Card, 120m, 0m, 15m, 15m, 0m, 0m, null, null, null, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Complaint Item", 1, 120m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
