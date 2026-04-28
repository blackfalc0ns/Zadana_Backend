using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Modules.Orders.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class CustomerOrderReadServiceTests
{
    [Fact]
    public async Task GetCustomerOrdersAsync_ShouldClassifyOrdersIntoBuckets()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        dbContext.Users.Add(user);

        dbContext.Orders.Add(CreateOrder(user.Id, OrderStatus.PendingVendorAcceptance, "ORD-ACTIVE"));
        dbContext.Orders.Add(CreateOrder(user.Id, OrderStatus.Delivered, "ORD-COMPLETED"));
        dbContext.Orders.Add(CreateOrder(user.Id, OrderStatus.Refunded, "ORD-RETURN"));
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var active = await service.GetCustomerOrdersAsync(user.Id, CustomerOrderBucket.Active, 1, 20);
        var completed = await service.GetCustomerOrdersAsync(user.Id, CustomerOrderBucket.Completed, 1, 20);
        var returns = await service.GetCustomerOrdersAsync(user.Id, CustomerOrderBucket.Returns, 1, 20);

        active.Items.Should().ContainSingle();
        active.Items[0].Status.Should().Be("pending");
        completed.Items.Should().ContainSingle();
        completed.Items[0].Status.Should().Be("delivered");
        returns.Items.Should().ContainSingle();
        returns.Items[0].Status.Should().Be("returning");
    }

    [Fact]
    public async Task GetCustomerOrderDetailAsync_ShouldMapCanCancelFromOrderStatus()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        dbContext.Users.Add(user);

        var cancellable = CreateOrder(user.Id, OrderStatus.Preparing, "ORD-CAN-CANCEL");
        var locked = CreateOrder(user.Id, OrderStatus.ReadyForPickup, "ORD-LOCKED");

        dbContext.Orders.AddRange(cancellable, locked);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var cancellableDetail = await service.GetCustomerOrderDetailAsync(cancellable.Id, user.Id);
        var lockedDetail = await service.GetCustomerOrderDetailAsync(locked.Id, user.Id);

        cancellableDetail!.CanCancel.Should().BeTrue();
        lockedDetail!.CanCancel.Should().BeFalse();
    }

    [Fact]
    public async Task GetCustomerOrdersAsync_ShouldExposePaymentActionsForPendingPaymentOrders()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        dbContext.Users.Add(user);

        var pendingCardOrder = CreateOrder(user.Id, OrderStatus.PendingPayment, "ORD-PAYMENT-PENDING", PaymentMethodType.Card);
        pendingCardOrder.UpdatePaymentStatus(PaymentStatus.Pending);

        var acceptedPaidOrder = CreateOrder(user.Id, OrderStatus.PendingVendorAcceptance, "ORD-PAID-ACTIVE", PaymentMethodType.Card);
        acceptedPaidOrder.UpdatePaymentStatus(PaymentStatus.Paid);

        dbContext.Orders.AddRange(pendingCardOrder, acceptedPaidOrder);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var active = await service.GetCustomerOrdersAsync(user.Id, CustomerOrderBucket.Active, 1, 20);
        var pendingItem = active.Items.Single(item => item.Id == pendingCardOrder.Id);
        var paidItem = active.Items.Single(item => item.Id == acceptedPaidOrder.Id);

        pendingItem.PaymentStatus.Should().Be("pending");
        pendingItem.PaymentMethod.Should().Be("card");
        pendingItem.CanRetryPayment.Should().BeTrue();
        pendingItem.CanDelete.Should().BeTrue();
        pendingItem.CanCancel.Should().BeFalse();

        paidItem.PaymentStatus.Should().Be("paid");
        paidItem.CanRetryPayment.Should().BeFalse();
        paidItem.CanDelete.Should().BeFalse();
        paidItem.CanCancel.Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.PendingVendorAcceptance, "pending")]
    [InlineData(OrderStatus.Accepted, "accepted")]
    [InlineData(OrderStatus.Preparing, "preparing")]
    [InlineData(OrderStatus.ReadyForPickup, "preparing")]
    public async Task GetCustomerOrderTrackingAsync_ShouldExposeGranularVendorTrackingStatuses(
        OrderStatus status,
        string expectedStatus)
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id, status, $"ORD-TRACK-{status}");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var result = await service.GetCustomerOrderTrackingAsync(order.Id, user.Id);

        result.Should().NotBeNull();
        result!.Order.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task GetCustomerOrderTrackingAsync_ShouldReturnDriverEtaAndTimelineForOutForDeliveryOrder()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateUser();
        var driverUser = new User("Driver User", "driver.orders@test.com", "01000000009", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Motorcycle, "12345678901234", "ABC-123");
        var order = CreateOrder(customer.Id, "ORD-TRACK-001", OrderStatus.Placed, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.PickedUp, OrderStatus.OnTheWay);
        var assignment = new DeliveryAssignment(order.Id, 0m);

        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkPickedUp();
        assignment.EnsureDeliveryOtp(TimeSpan.FromHours(4));

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var result = await service.GetCustomerOrderTrackingAsync(order.Id, customer.Id);

        result.Should().NotBeNull();
        result!.Order.Status.Should().Be("out_for_delivery");
        result.Driver.Should().NotBeNull();
        result.Driver!.Name.Should().Be("Driver User");
        result.Driver.PhoneNumber.Should().Be("01000000009");
        result.Driver.Subtitle.Should().Be("Motorcycle");
        result.AssignedDriver.Should().NotBeNull();
        result.ShowDeliveryOtp.Should().BeTrue();
        result.DeliveryOtp.Should().Be(assignment.DeliveryOtpCode);
        result.EstimatedDelivery.Should().NotBeNull();
        result.Timeline.Should().HaveCount(5);
        result.Timeline[3].Id.Should().Be("out_for_delivery");
        result.Timeline[3].IsActive.Should().BeTrue();
        result.Timeline[0].IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetCustomerOrderTrackingAsync_ShouldExposeArrivalStateWhenDriverReachedCustomer()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateUser();
        var driverUser = new User("Driver User", "driver.arrival.customer@test.com", "01000000022", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Motorcycle, "12345678901235", "ABC-124");
        var order = CreateOrder(customer.Id, "ORD-TRACK-003", OrderStatus.Placed, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.PickedUp, OrderStatus.OnTheWay);
        var assignment = new DeliveryAssignment(order.Id, 0m);

        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkPickedUp();
        assignment.MarkArrivedAtCustomer();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);
        var result = await service.GetCustomerOrderTrackingAsync(order.Id, customer.Id);

        result.Should().NotBeNull();
        result!.DriverArrivalState.Should().Be("arrived_at_customer");
        result.DriverArrivalUpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCustomerOrderTrackingAsync_ShouldReturnCancelledStateWithoutEta()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id, "ORD-TRACK-002", OrderStatus.Placed, OrderStatus.Accepted, OrderStatus.Cancelled);

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var result = await service.GetCustomerOrderTrackingAsync(order.Id, user.Id);

        result.Should().NotBeNull();
        result!.Order.Status.Should().Be("cancelled");
        result.EstimatedDelivery.Should().BeNull();
        result.Driver.Should().BeNull();
        result.Timeline.Should().HaveCount(5);
        result.Timeline[^1].Id.Should().Be("cancelled");
        result.Timeline[^1].IsActive.Should().BeTrue();
        result.Timeline[^1].Time.Should().NotBeNullOrWhiteSpace();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateUser() =>
        new("Customer User", "customer.orders@test.com", "01000000000", UserRole.Customer);

    private static Order CreateOrder(Guid userId, OrderStatus status, string orderNumber, PaymentMethodType paymentMethod = PaymentMethodType.CashOnDelivery)
    {
        var order = new Order(orderNumber, userId, Guid.NewGuid(), Guid.NewGuid(), paymentMethod, 100m, 0m, 10m, 10m, 0m, 0m, null, null, null, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Fresh Item", 2, 50m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }

    private static Order CreateOrder(Guid userId, string orderNumber, params OrderStatus[] statuses)
    {
        var order = new Order(orderNumber, userId, Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.CashOnDelivery, 100m, 0m, 10m, 10m, 0m, 0m, null, null, null, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Fresh Item", 2, 50m));

        foreach (var status in statuses)
        {
            if (order.Status != status)
            {
                order.ChangeStatus(status);
            }
        }

        return order;
    }
}
