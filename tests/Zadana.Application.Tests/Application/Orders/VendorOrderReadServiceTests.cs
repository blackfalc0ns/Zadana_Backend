using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Modules.Orders.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class VendorOrderReadServiceTests
{
    [Fact]
    public async Task GetVendorWorkspaceOrdersAsync_ShouldExcludePendingPaymentOrders()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var vendor = CreateVendor();
        var vendorId = vendor.Id;

        dbContext.Users.Add(customer);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(CreateOrder(customer.Id, vendorId, OrderStatus.PendingPayment, "ORD-PAYMENT-PENDING"));
        dbContext.Orders.Add(CreateOrder(customer.Id, vendorId, OrderStatus.PendingVendorAcceptance, "ORD-VISIBLE"));
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var result = await service.GetVendorWorkspaceOrdersAsync(vendorId, null, null, null, 1, 20);

        result.Items.Should().ContainSingle();
        result.Items[0].OrderNumber.Should().Be("ORD-VISIBLE");
    }

    [Fact]
    public async Task GetVendorOrderDetailAsync_ShouldReturnNullForPendingPaymentOrder()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var vendor = CreateVendor();
        var vendorId = vendor.Id;
        var pendingPaymentOrder = CreateOrder(customer.Id, vendorId, OrderStatus.PendingPayment, "ORD-HIDDEN");

        dbContext.Users.Add(customer);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(pendingPaymentOrder);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var result = await service.GetVendorOrderDetailAsync(vendorId, pendingPaymentOrder.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetVendorOrderDetailAsync_ShouldExposeAssignedDriverAndPickupOtp()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var vendor = CreateVendor();
        var vendorId = vendor.Id;
        var driverUser = new User("Vendor Detail Driver", "vendor.driver@test.com", "01000000021", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567898", "CAR-777");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.DriverAssigned, "ORD-WITH-DRIVER");
        var assignment = new DeliveryAssignment(order.Id, 0m);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(4));

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);

        var result = await service.GetVendorOrderDetailAsync(vendorId, order.Id);

        result.Should().NotBeNull();
        result!.AssignedDriver.Should().NotBeNull();
        result.AssignedDriver!.Name.Should().Be("Vendor Detail Driver");
        result.PickupOtp.Should().Be(assignment.PickupOtpCode);
        result.CanConfirmPickup.Should().BeTrue();
        result.PickupOtpStatus.Should().Be("pending");
    }

    [Fact]
    public async Task GetVendorOrderDetailAsync_ShouldExposeArrivalStateWhenDriverReachedStore()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var vendor = CreateVendor();
        var vendorId = vendor.Id;
        var driverUser = new User("Vendor Arrival Driver", "vendor.arrival.driver@test.com", "01000000023", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567899", "CAR-778");
        driver.Approve(Guid.NewGuid());

        var order = CreateOrder(customer.Id, vendorId, OrderStatus.DriverAssigned, "ORD-ARRIVED-STORE");
        var assignment = new DeliveryAssignment(order.Id, 0m);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkArrivedAtVendor();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var service = new OrderReadService(dbContext);
        var result = await service.GetVendorOrderDetailAsync(vendorId, order.Id);

        result.Should().NotBeNull();
        result!.DriverArrivalState.Should().Be("arrived_at_vendor");
        result.DriverArrivalUpdatedAtUtc.Should().NotBeNull();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateCustomer() =>
        new("Customer User", "vendor.orders.customer@test.com", "01000000011", UserRole.Customer);

    private static Vendor CreateVendor()
    {
        return new Vendor(
            Guid.NewGuid(),
            "متجر اختبار",
            "Vendor Test Store",
            "Groceries",
            $"CR-{Guid.NewGuid():N}".Substring(0, 12),
            $"vendor-{Guid.NewGuid():N}@test.com",
            "01000000031",
            city: "Riyadh",
            nationalAddress: "Olaya");
    }

    private static Order CreateOrder(Guid userId, Guid vendorId, OrderStatus status, string orderNumber)
    {
        var order = new Order(orderNumber, userId, vendorId, Guid.NewGuid(), PaymentMethodType.Card, 100m, 0m, 10m, 10m, 0m, 0m, null, null, null, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Fresh Item", 2, 50m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
