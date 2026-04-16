using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Orders.Commands.CancelCustomerOrder;
using Zadana.Application.Modules.Orders.Commands.CreateOrderComplaint;
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
        var order = CreateOrder(user.Id, OrderStatus.DriverAssigned, "ORD-CANCEL-002");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var handler = new CancelCustomerOrderCommandHandler(dbContext, dbContext);

        var act = () => handler.Handle(
            new CancelCustomerOrderCommand(order.Id, user.Id, "Changed my mind", null),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "ORDER_CANNOT_BE_CANCELLED");
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

        var handler = new CreateOrderComplaintCommandHandler(dbContext, dbContext);

        var result = await handler.Handle(
            new CreateOrderComplaintCommand(
                order.Id,
                user.Id,
                "Order arrived damaged",
                [new CreateOrderComplaintAttachmentItem("photo.jpg", "https://cdn.example.com/photo.jpg")]),
            CancellationToken.None);

        result.Status.Should().Be("submitted");
        dbContext.OrderComplaints.Should().ContainSingle();
        dbContext.OrderComplaintAttachments.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateOrderComplaint_ShouldRejectDuplicateComplaint()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id, OrderStatus.Delivered, "ORD-COMPLAINT-002");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.OrderComplaints.Add(new OrderComplaint(order.Id, "Existing complaint"));
        await dbContext.SaveChangesAsync();

        var handler = new CreateOrderComplaintCommandHandler(dbContext, dbContext);

        var act = () => handler.Handle(
            new CreateOrderComplaintCommand(order.Id, user.Id, "Another complaint", []),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "ORDER_COMPLAINT_ALREADY_EXISTS");
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

    private static Order CreateOrder(Guid userId, OrderStatus status, string orderNumber)
    {
        var order = new Order(orderNumber, userId, Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.Card, 120m, 0m, 15m, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Complaint Item", 1, 120m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
