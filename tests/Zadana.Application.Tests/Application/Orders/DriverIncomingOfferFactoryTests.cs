using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverIncomingOfferFactoryTests
{
    [Fact]
    public async Task Build_ShouldMatchHomeOfferShape()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer", "customer.offer@test.com", "01000000010", UserRole.Customer);
        var vendorUser = new User("Vendor", "vendor.offer@test.com", "01000000011", UserRole.Vendor);
        var vendor = new Vendor(
            vendorUser.Id,
            "متجر الاختبار",
            "Dispatch Store",
            "Groceries",
            "CR-100",
            "vendor.offer@test.com",
            "01000000011",
            logoUrl: "https://example.com/logo.png");
        var branch = new VendorBranch(vendor.Id, "Olaya Branch", "King Fahd Rd", 24.7137m, 46.6754m, "01000000012", 8m);
        var address = new CustomerAddress(
            customer.Id,
            "Customer Name",
            "01000000013",
            "King Abdullah Road",
            buildingNo: "15",
            floorNo: "3",
            apartmentNo: "8",
            city: "Riyadh",
            area: "Olaya",
            latitude: 24.7236m,
            longitude: 46.6853m);
        var order = new Order(
            "ORD-OFFER-001",
            customer.Id,
            vendor.Id,
            address.Id,
            PaymentMethodType.CashOnDelivery,
            120m,
            0m,
            15m,
            15m,
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
            5m,
            vendorBranchId: branch.Id);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Dispatch Product", 2, 120m));

        var assignment = new DeliveryAssignment(order.Id, 120m);
        assignment.OfferTo(Guid.NewGuid(), 1, DateTime.UtcNow.AddSeconds(45));

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var loadedAssignment = await dbContext.DeliveryAssignments
            .Include(item => item.Order)
                .ThenInclude(o => o.Vendor)
            .Include(item => item.Order)
                .ThenInclude(o => o.VendorBranch)
            .Include(item => item.Order)
                .ThenInclude(o => o.Items)
            .SingleAsync();

        var offer = DriverIncomingOfferFactory.Build(loadedAssignment, address, DateTime.UtcNow);

        offer.AssignmentId.Should().Be(assignment.Id);
        offer.OrderNumber.Should().Be("ORD-OFFER-001");
        offer.VendorName.Should().Be("Dispatch Store");
        offer.VendorNameAr.Should().Be("متجر الاختبار");
        offer.VendorNameEn.Should().Be("Dispatch Store");
        offer.VendorLogoUrl.Should().Be("https://example.com/logo.png");
        offer.PickupAddress.Should().Be("King Fahd Rd");
        offer.CustomerName.Should().Be("Customer Name");
        offer.DeliveryAddress.Should().Be("King Abdullah Road, Building 15, Floor 3, Apartment 8, Olaya, Riyadh");
        offer.Payout.Should().Be(15m);
        offer.TotalAmount.Should().Be(135m);
        offer.CodAmount.Should().Be(120m);
        offer.CountdownSeconds.Should().BeInRange(30, 45);
        offer.OrderItems.Should().ContainSingle();
        offer.OrderItems[0].Name.Should().Be("Dispatch Product");
        offer.OrderItems[0].Quantity.Should().Be(2);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
