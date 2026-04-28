using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverReadServiceTests
{
    [Fact]
    public async Task GetAssignmentDetailAsync_ShouldReturnOperationalSnapshotForDriver()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var driverUser = new User("Driver Detail User", "driver.detail@test.com", "01000000055", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Motorcycle, "12345678901234", "LIC-100", "Riyadh")
        {
        };
        var vendor = CreateVendor();
        var branch = CreateBranch(vendor.Id);
        var address = CreateCustomerAddress(customer.Id);
        var order = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.DriverAssigned, "ORD-DETAIL-01");
        var assignment = new DeliveryAssignment(order.Id, 60m);

        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkArrivedAtVendor();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(2));

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetAssignmentDetailAsync(driver.Id, assignment.Id);

        result.Should().NotBeNull();
        result!.AssignmentStatus.Should().Be(nameof(AssignmentStatus.ArrivedAtVendor));
        result.HomeState.Should().Be("OnMission");
        result.AllowedActions.Should().BeEmpty();
        result.PickupOtpRequired.Should().BeTrue();
        result.PickupOtpStatus.Should().Be("pending");
        result.DriverArrivalState.Should().Be("arrived_at_vendor");
        result.CustomerPhone.Should().Be(address.ContactPhone);
        result.OrderItems.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAssignmentDetailAsync_AfterPickupHandoff_ShouldExposeOnTheWayAction()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var driverUser = new User("Driver Detail User", "driver.detail.ontheway@test.com", "01000000062", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Motorcycle, "12345678901237", "LIC-103", "Riyadh");
        var vendor = CreateVendor();
        var branch = CreateBranch(vendor.Id);
        var address = CreateCustomerAddress(customer.Id);
        var order = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.PickedUp, "ORD-DETAIL-02");
        var assignment = new DeliveryAssignment(order.Id, 60m);

        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkArrivedAtVendor();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(2));
        assignment.VerifyPickupOtp(driver.Id, assignment.PickupOtpCode!);
        assignment.MarkPickedUp();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetAssignmentDetailAsync(driver.Id, assignment.Id);

        result.Should().NotBeNull();
        result!.AssignmentStatus.Should().Be(nameof(AssignmentStatus.PickedUp));
        result.AllowedActions.Should().ContainSingle().Which.Should().Be("mark_on_the_way");
        result.PickupOtpRequired.Should().BeFalse();
        result.PickupOtpStatus.Should().Be("verified");
        result.PickupOtpCode.Should().BeNull();
    }

    [Fact]
    public async Task GetCompletedOrdersAsync_ShouldReturnOnlyCompletedAndRespectFilter()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var driverUser = new User("Driver Completed User", "driver.completed@test.com", "01000000056", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "12345678901235", "LIC-101", "Riyadh");
        var vendor = CreateVendor();
        var branch = CreateBranch(vendor.Id);
        var address = CreateCustomerAddress(customer.Id);

        var deliveredOrder = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.Delivered, "ORD-COMP-1");
        var cancelledOrder = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.Cancelled, "ORD-COMP-2");
        var failedOrder = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.DeliveryFailed, "ORD-COMP-3");
        var activeOrder = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.DriverAssigned, "ORD-ACTIVE-1");

        var deliveredAssignment = CreateCompletedAssignment(driver.Id, deliveredOrder.Id, AssignmentStatus.Delivered);
        var cancelledAssignment = CreateCompletedAssignment(driver.Id, cancelledOrder.Id, AssignmentStatus.Accepted);
        var failedAssignment = CreateCompletedAssignment(driver.Id, failedOrder.Id, AssignmentStatus.Failed);
        var activeAssignment = CreateCompletedAssignment(driver.Id, activeOrder.Id, AssignmentStatus.Accepted);

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.AddRange(deliveredOrder, cancelledOrder, failedOrder, activeOrder);
        dbContext.DeliveryAssignments.AddRange(deliveredAssignment, cancelledAssignment, failedAssignment, activeAssignment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var allCompleted = await service.GetCompletedOrdersAsync(driver.Id);
        var deliveredOnly = await service.GetCompletedOrdersAsync(driver.Id, "delivered");

        allCompleted.TotalCount.Should().Be(3);
        allCompleted.Items.Select(x => x.Status).Should().BeEquivalentTo(["delivered", "cancelled", "deliveryFailed"]);
        deliveredOnly.TotalCount.Should().Be(1);
        deliveredOnly.Items[0].Status.Should().Be("delivered");
    }

    [Fact]
    public async Task GetDriverProfileAsync_ShouldCalculateCompletionAndMissingRequirements()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Profile User", "driver.profile@test.com", "01000000057", UserRole.Driver);
        var driver = new Driver(user.Id, DriverVehicleType.Car, "12345678901236", "LIC-102");

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.IsProfileComplete.Should().BeFalse();
        result.CompletionPercent.Should().Be(25);
        result.MissingRequirements.Should().Contain("missing_personal_info");
        result.MissingRequirements.Should().Contain("missing_documents");
        result.MissingRequirements.Should().Contain("missing_zone_selection");
        result.CanSubmitForReview.Should().BeFalse();
    }

    private static DriverReadService CreateService(ApplicationDbContext dbContext)
    {
        var commitmentPolicy = new Mock<IDriverCommitmentPolicyService>();
        commitmentPolicy
            .Setup(x => x.GetDriverSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverCommitmentSummaryDto(0, 0, 0, 0, 0, 100m, "Healthy", true, null, null));

        commitmentPolicy
            .Setup(x => x.GetDriverSummariesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DriverCommitmentSummaryDto>());

        return new DriverReadService(dbContext, commitmentPolicy.Object);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateCustomer() =>
        new("Customer User", "driver.read.customer@test.com", "01000000058", UserRole.Customer);

    private static Vendor CreateVendor() =>
        new(
            Guid.NewGuid(),
            "متجر تجريبي",
            "Driver Read Vendor",
            "Groceries",
            $"CR-{Guid.NewGuid():N}".Substring(0, 12),
            $"vendor-{Guid.NewGuid():N}@test.com",
            "01000000059",
            city: "Riyadh",
            nationalAddress: "Olaya");

    private static VendorBranch CreateBranch(Guid vendorId) =>
        new(vendorId, "Main Branch", "Olaya Street", 24.7136m, 46.6753m, "01000000060", 12m);

    private static CustomerAddress CreateCustomerAddress(Guid userId) =>
        new(userId, "Ahmed Customer", "01000000061", "Yasmin District", AddressLabel.Home, city: "Riyadh", area: "Yasmin", latitude: 24.7821m, longitude: 46.6520m);

    private static Order CreateOrder(
        Guid userId,
        Guid vendorId,
        Guid vendorBranchId,
        Guid customerAddressId,
        OrderStatus status,
        string orderNumber)
    {
        var order = new Order(
            orderNumber,
            userId,
            vendorId,
            customerAddressId,
            PaymentMethodType.CashOnDelivery,
            100m,
            0m,
            12m,
            10m,
            2m,
            0m,
            4.6m,
            "exact-distance",
            "Riyadh Standard",
            5m,
            vendorBranchId: vendorBranchId);

        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Fresh Item", 2, 50m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }

    private static DeliveryAssignment CreateCompletedAssignment(Guid driverId, Guid orderId, AssignmentStatus status)
    {
        var assignment = new DeliveryAssignment(orderId, 50m);
        assignment.OfferTo(driverId, 1, DateTime.UtcNow.AddMinutes(5));

        if (status != AssignmentStatus.Cancelled)
        {
            assignment.Accept();
        }

        return status switch
        {
            AssignmentStatus.Delivered => MarkDelivered(assignment),
            AssignmentStatus.Failed => MarkFailed(assignment),
            _ => assignment
        };
    }

    private static DeliveryAssignment MarkDelivered(DeliveryAssignment assignment)
    {
        assignment.MarkPickedUp();
        assignment.MarkDelivered();
        return assignment;
    }

    private static DeliveryAssignment MarkFailed(DeliveryAssignment assignment)
    {
        assignment.Fail("delivery failed");
        return assignment;
    }

}
