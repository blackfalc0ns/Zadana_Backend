using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using Zadana.Application.Modules.Checkout.Support;

using Zadana.Application.Modules.Delivery.Interfaces;

using Zadana.Application.Modules.Delivery.Support;

using Zadana.Application.Modules.Geography.Support;

using Zadana.Domain.Modules.Geography.Entities;

using Zadana.Domain.Modules.Identity.Entities;

using Zadana.Domain.Modules.Identity.Enums;

using Zadana.Domain.Modules.Orders.Entities;

using Zadana.Domain.Modules.Orders.Enums;

using Zadana.Domain.Modules.Vendors.Entities;

using Zadana.Infrastructure.Persistence;

using Zadana.Infrastructure.Persistence.Interceptors;



namespace Zadana.Application.Tests.Application.Checkout;



public class CheckoutSupportTests

{

    [Fact]

    public async Task EvaluateDeliveryAsync_WhenCustomerOutsideBranchDeliveryRadius_ShouldBeUndeliverable()

    {

        await using var dbContext = CreateDbContext();

        await SeedEasternOperationalGeographyAsync(dbContext);

        var customer = new User("Eastern Customer", "checkout.eastern.customer@test.com", "01000000200", UserRole.Customer);

        var vendorUser = new User("Eastern Vendor", "checkout.eastern.vendor@test.com", "01000000201", UserRole.Vendor);

        var vendor = new Vendor(

            vendorUser.Id,

            "متجر الشرقية",

            "Eastern Checkout Store",

            "Groceries",

            "1234567899",

            "checkout.eastern.vendor@test.com",

            "01000000201",

            city: "Dammam");

        vendor.Approve(10m, Guid.NewGuid());



        var branch = new VendorBranch(

            vendor.Id,

            "Dammam Branch",

            "BR-DMM",

            isPrimary: true,

            "King Fahd Road",

            "EASTERN",

            "DAMMAM",

            latitude: 26.43m,

            longitude: 50.08m,

            contactPhone: "01000000202",

            managerName: "Branch Manager",

            managerContact: "01000000203",

            deliveryRadiusKm: 5m);

        var address = new CustomerAddress(

            customer.Id,

            "Eastern Customer",

            "01000000200",

            "Corniche",

            AddressLabel.Home,

            city: "Khobar",

            latitude: 26.2172m,

            longitude: 50.1971m);



        dbContext.Users.AddRange(customer, vendorUser);

        dbContext.Vendors.Add(vendor);

        dbContext.VendorBranches.Add(branch);

        dbContext.CustomerAddresses.Add(address);

        await dbContext.SaveChangesAsync();



        const decimal vendorToCustomerDistanceKm = 24.5m;

        vendorToCustomerDistanceKm.Should().BeGreaterThan(branch.DeliveryRadiusKm);



        var deliveryPricingMock = new Mock<IDeliveryPricingService>();

        deliveryPricingMock

            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))

            .ReturnsAsync(new DeliveryPriceQuote(

                10m,

                5m,

                0m,

                15m,

                vendorToCustomerDistanceKm,

                "zone",

                "Eastern proximity",

                1m,

                vendorToCustomerDistanceKm,

                3m,

                12m,

                "driver",

                "vendor",

                false,

                "manual",

                null,

                "locked",

                DateTime.UtcNow,

                1,

                false));



        var assessment = await CheckoutSupport.EvaluateDeliveryAsync(

            dbContext,

            deliveryPricingMock.Object,

            branch.Id,

            address,

            CancellationToken.None);



        assessment.DeliveryCheck.Status.Should().Be("undeliverable");

        assessment.DeliveryCheck.IsDeliverable.Should().BeFalse();

        assessment.DeliveryCheck.DeliveryFee.Should().BeNull();

    }



    [Fact]

    public async Task EvaluateDeliveryAsync_WhenCustomerBeyondFiftyKm_ShouldBeUndeliverable()

    {

        await using var dbContext = CreateDbContext();

        await SeedEasternOperationalGeographyAsync(dbContext);

        var customer = new User("Far Customer", "checkout.far.customer@test.com", "01000000210", UserRole.Customer);

        var vendorUser = new User("Far Vendor", "checkout.far.vendor@test.com", "01000000211", UserRole.Vendor);

        var vendor = new Vendor(

            vendorUser.Id,

            "متجر بعيد",

            "Far Checkout Store",

            "Groceries",

            "1234567888",

            "checkout.far.vendor@test.com",

            "01000000211",

            city: "Khobar");

        vendor.Approve(10m, Guid.NewGuid());



        var branch = new VendorBranch(

            vendor.Id,

            "Khobar Branch",

            "BR-KHB",

            isPrimary: true,

            "Corniche Road",

            "EASTERN",

            "KHOBAR",

            latitude: 26.22m,

            longitude: 50.19m,

            contactPhone: "01000000212",

            managerName: "Branch Manager",

            managerContact: "01000000213",

            deliveryRadiusKm: 100m);

        var address = new CustomerAddress(

            customer.Id,

            "Far Customer",

            "01000000210",

            "Olaya",

            AddressLabel.Home,

            city: "Riyadh",

            latitude: 24.71m,

            longitude: 46.67m);



        dbContext.Users.AddRange(customer, vendorUser);

        dbContext.Vendors.Add(vendor);

        dbContext.VendorBranches.Add(branch);

        dbContext.CustomerAddresses.Add(address);

        await dbContext.SaveChangesAsync();



        const decimal vendorToCustomerDistanceKm = 55m;

        vendorToCustomerDistanceKm.Should().BeGreaterThan(DeliveryProximityLimits.MaxMatchKm);



        var deliveryPricingMock = new Mock<IDeliveryPricingService>();

        deliveryPricingMock

            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))

            .ReturnsAsync(new DeliveryPriceQuote(

                10m,

                5m,

                0m,

                15m,

                vendorToCustomerDistanceKm,

                "zone",

                "Beyond platform radius",

                1m,

                vendorToCustomerDistanceKm,

                3m,

                12m,

                "driver",

                "vendor",

                false,

                "manual",

                null,

                "locked",

                DateTime.UtcNow,

                1,

                false));



        var assessment = await CheckoutSupport.EvaluateDeliveryAsync(

            dbContext,

            deliveryPricingMock.Object,

            branch.Id,

            address,

            CancellationToken.None);



        assessment.DeliveryCheck.Status.Should().Be("service_area_unavailable");

        assessment.DeliveryCheck.IsDeliverable.Should().BeFalse();

    }



    [Fact]

    public async Task EvaluateDeliveryAsync_WhenPricingAnomaly_ShouldBlockCheckout()

    {

        await using var dbContext = CreateDbContext();

        await SeedEasternOperationalGeographyAsync(dbContext);

        var customer = new User("Anomaly Customer", "checkout.anomaly.customer@test.com", "01000000225", UserRole.Customer);

        var vendorUser = new User("Anomaly Vendor", "checkout.anomaly.vendor@test.com", "01000000226", UserRole.Vendor);

        var vendor = new Vendor(

            vendorUser.Id,

            "متجر",

            "Anomaly Store",

            "Groceries",

            "1234567870",

            "checkout.anomaly.vendor@test.com",

            "01000000226",

            city: "Khobar");

        vendor.Approve(10m, Guid.NewGuid());



        var branch = new VendorBranch(

            vendor.Id,

            "Khobar Branch",

            "BR-ANOMALY",

            isPrimary: true,

            "Corniche Road",

            "EASTERN",

            "KHOBAR",

            latitude: 26.22m,

            longitude: 50.19m,

            contactPhone: "01000000227",

            managerName: "Branch Manager",

            managerContact: "01000000228",

            deliveryRadiusKm: 50m);

        var address = new CustomerAddress(

            customer.Id,

            "Anomaly Customer",

            "01000000225",

            "Home",

            AddressLabel.Home,

            city: "Khobar",

            latitude: 26.2172m,

            longitude: 50.1971m);



        dbContext.Users.AddRange(customer, vendorUser);

        dbContext.Vendors.Add(vendor);

        dbContext.VendorBranches.Add(branch);

        dbContext.CustomerAddresses.Add(address);

        await dbContext.SaveChangesAsync();



        var deliveryPricingMock = new Mock<IDeliveryPricingService>();

        deliveryPricingMock

            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))

            .ReturnsAsync(new DeliveryPriceQuote(

                10m,

                5m,

                0m,

                15m,

                2m,

                "zone",

                "Anomaly quote",

                1m,

                1m,

                3m,

                12m,

                "driver",

                "vendor",

                false,

                "manual",

                null,

                "locked",

                DateTime.UtcNow,

                1,

                HasAnomalyWarning: true));



        var assessment = await CheckoutSupport.EvaluateDeliveryAsync(

            dbContext,

            deliveryPricingMock.Object,

            branch.Id,

            address,

            CancellationToken.None);



        assessment.DeliveryCheck.Status.Should().Be("pricing_anomaly");

        assessment.DeliveryCheck.CanProceedToCheckout.Should().BeFalse();

    }



    [Fact]

    public async Task EvaluateDeliveryAsync_WhenSameCityButBeyondFiftyKm_ShouldBeUndeliverable()

    {

        await using var dbContext = CreateDbContext();

        await SeedEasternOperationalGeographyAsync(dbContext);

        var customer = new User("Same City Customer", "checkout.samecity.customer@test.com", "01000000220", UserRole.Customer);

        var vendorUser = new User("Same City Vendor", "checkout.samecity.vendor@test.com", "01000000221", UserRole.Vendor);

        var vendor = new Vendor(

            vendorUser.Id,

            "متجر",

            "Same City Store",

            "Groceries",

            "1234567877",

            "checkout.samecity.vendor@test.com",

            "01000000221",

            city: "Dammam");

        vendor.Approve(10m, Guid.NewGuid());



        var branch = new VendorBranch(

            vendor.Id,

            "Dammam Branch",

            "BR-DMM-2",

            isPrimary: true,

            "King Fahd Road",

            "EASTERN",

            "DAMMAM",

            latitude: 26.43m,

            longitude: 50.08m,

            contactPhone: "01000000222",

            managerName: "Branch Manager",

            managerContact: "01000000223",

            deliveryRadiusKm: 100m);

        var address = new CustomerAddress(

            customer.Id,

            "Same City Customer",

            "01000000220",

            "Far District",

            AddressLabel.Home,

            city: "Dammam",

            latitude: 26.62m,

            longitude: 50.19m);



        dbContext.Users.AddRange(customer, vendorUser);

        dbContext.Vendors.Add(vendor);

        dbContext.VendorBranches.Add(branch);

        dbContext.CustomerAddresses.Add(address);

        await dbContext.SaveChangesAsync();



        const decimal vendorToCustomerDistanceKm = 52m;

        vendorToCustomerDistanceKm.Should().BeGreaterThan(DeliveryProximityLimits.MaxMatchKm);



        var deliveryPricingMock = new Mock<IDeliveryPricingService>();

        deliveryPricingMock

            .Setup(service => service.QuoteAsync(branch.Id, address.Id, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))

            .ReturnsAsync(new DeliveryPriceQuote(

                10m,

                5m,

                0m,

                15m,

                vendorToCustomerDistanceKm,

                "zone",

                "Same city beyond platform radius",

                1m,

                vendorToCustomerDistanceKm,

                3m,

                12m,

                "driver",

                "vendor",

                false,

                "manual",

                null,

                "locked",

                DateTime.UtcNow,

                1,

                false));



        var assessment = await CheckoutSupport.EvaluateDeliveryAsync(

            dbContext,

            deliveryPricingMock.Object,

            branch.Id,

            address,

            CancellationToken.None);



        assessment.DeliveryCheck.Status.Should().Be("undeliverable");

        assessment.DeliveryCheck.IsDeliverable.Should().BeFalse();

    }



    [Fact]

    public async Task EvaluatePickupAsync_WhenNearestBranchWithinFiftyKm_ShouldBeReady()

    {

        await using var dbContext = CreateDbContext();

        await SeedEasternOperationalGeographyAsync(dbContext);

        var customer = new User("Pickup Customer", "checkout.pickup.customer@test.com", "01000000230", UserRole.Customer);

        var vendorUser = new User("Pickup Vendor", "checkout.pickup.vendor@test.com", "01000000231", UserRole.Vendor);

        var vendor = new Vendor(

            vendorUser.Id,

            "متجر استلام",

            "Pickup Store",

            "Groceries",

            "1234567866",

            "checkout.pickup.vendor@test.com",

            "01000000231",

            city: "Khobar");

        vendor.Approve(10m, Guid.NewGuid());



        var branch = new VendorBranch(

            vendor.Id,

            "Khobar Branch",

            "BR-PICKUP",

            isPrimary: true,

            "Corniche Road",

            "EASTERN",

            "KHOBAR",

            latitude: 26.22m,

            longitude: 50.19m,

            contactPhone: "01000000232",

            managerName: "Branch Manager",

            managerContact: "01000000233",

            deliveryRadiusKm: 100m);

        var address = new CustomerAddress(

            customer.Id,

            "Pickup Customer",

            "01000000230",

            "Home",

            AddressLabel.Home,

            city: "Khobar",

            latitude: 26.2172m,

            longitude: 50.1971m);

        var cart = new Cart(customer.Id);

        dbContext.Users.AddRange(customer, vendorUser);

        dbContext.Vendors.Add(vendor);

        dbContext.VendorBranches.Add(branch);

        dbContext.CustomerAddresses.Add(address);

        dbContext.Carts.Add(cart);

        await dbContext.SaveChangesAsync();



        var assessment = await CheckoutSupport.EvaluatePickupAsync(

            dbContext,

            cart,

            vendor.Id,

            pickupBranchId: null,

            address,

            CancellationToken.None);



        assessment.BranchId.Should().Be(branch.Id);

        assessment.DeliveryCheck.Status.Should().Be("pickup_ready");

        assessment.DeliveryCheck.CanProceedToCheckout.Should().BeTrue();

    }



    [Fact]

    public async Task EvaluatePickupAsync_WhenArabicCityName_ShouldResolveOperationalGeography()

    {

        await using var dbContext = CreateDbContext();

        await SeedEasternOperationalGeographyAsync(dbContext);

        var customer = new User("Arabic Pickup Customer", "checkout.pickup.ar.customer@test.com", "01000000250", UserRole.Customer);

        var vendorUser = new User("Arabic Pickup Vendor", "checkout.pickup.ar.vendor@test.com", "01000000251", UserRole.Vendor);

        var vendor = new Vendor(

            vendorUser.Id,

            "متجر الظهران",

            "Dhahran Store",

            "Groceries",

            "1234567855",

            "checkout.pickup.ar.vendor@test.com",

            "01000000251",

            city: "الظهران");

        vendor.Approve(10m, Guid.NewGuid());



        var branch = new VendorBranch(

            vendor.Id,

            "بقالة الأمل -2",

            "BR-DHA",

            isPrimary: false,

            "مركز المدينة",

            "EASTERN",

            "الظهران",

            latitude: 26.2361m,

            longitude: 50.0393m,

            contactPhone: "01000000252",

            managerName: "Branch Manager",

            managerContact: "01000000253",

            deliveryRadiusKm: 20m);

        var address = new CustomerAddress(

            customer.Id,

            "Arabic Pickup Customer",

            "01000000250",

            "Home",

            AddressLabel.Home,

            city: "الظهران",

            latitude: 26.2361m,

            longitude: 50.0393m);

        var cart = new Cart(customer.Id);

        dbContext.Users.AddRange(customer, vendorUser);

        dbContext.Vendors.Add(vendor);

        dbContext.VendorBranches.Add(branch);

        dbContext.CustomerAddresses.Add(address);

        dbContext.Carts.Add(cart);

        await dbContext.SaveChangesAsync();



        (await OperationalGeographyScope.IsOperationalAddressCityAsync(dbContext, "الظهران", CancellationToken.None))

            .Should().BeTrue();



        var assessment = await CheckoutSupport.EvaluatePickupAsync(

            dbContext,

            cart,

            vendor.Id,

            pickupBranchId: branch.Id,

            address,

            CancellationToken.None);



        assessment.BranchId.Should().Be(branch.Id);

        assessment.DeliveryCheck.Status.Should().Be("pickup_ready");

    }



    [Fact]

    public async Task EvaluatePickupAsync_WhenExplicitBranchWithoutAddress_ShouldBeReady()

    {

        await using var dbContext = CreateDbContext();

        await SeedEasternOperationalGeographyAsync(dbContext);

        var customer = new User("No Address Pickup", "checkout.pickup.noaddr.customer@test.com", "01000000260", UserRole.Customer);

        var vendorUser = new User("No Address Vendor", "checkout.pickup.noaddr.vendor@test.com", "01000000261", UserRole.Vendor);

        var vendor = new Vendor(

            vendorUser.Id,

            "متجر بدون عنوان",

            "No Address Store",

            "Groceries",

            "1234567844",

            "checkout.pickup.noaddr.vendor@test.com",

            "01000000261",

            city: "Dammam");

        vendor.Approve(10m, Guid.NewGuid());



        var branch = new VendorBranch(

            vendor.Id,

            "Dammam Branch",

            "BR-NOADDR",

            isPrimary: true,

            "King Fahd Road",

            "EASTERN",

            "DAMMAM",

            latitude: 26.43m,

            longitude: 50.08m,

            contactPhone: "01000000262",

            managerName: "Branch Manager",

            managerContact: "01000000263",

            deliveryRadiusKm: 20m);

        var cart = new Cart(customer.Id);

        dbContext.Users.AddRange(customer, vendorUser);

        dbContext.Vendors.Add(vendor);

        dbContext.VendorBranches.Add(branch);

        dbContext.Carts.Add(cart);

        await dbContext.SaveChangesAsync();



        var assessment = await CheckoutSupport.EvaluatePickupAsync(

            dbContext,

            cart,

            vendor.Id,

            pickupBranchId: branch.Id,

            address: null,

            CancellationToken.None);



        assessment.BranchId.Should().Be(branch.Id);

        assessment.DeliveryCheck.Status.Should().Be("pickup_ready");

    }



    [Fact]

    public async Task EvaluatePickupAsync_WhenNoBranchWithinFiftyKm_ShouldBeUnavailable()

    {

        await using var dbContext = CreateDbContext();

        await SeedEasternOperationalGeographyAsync(dbContext);

        var customer = new User("Far Pickup Customer", "checkout.pickup.far.customer@test.com", "01000000240", UserRole.Customer);

        var vendorUser = new User("Far Pickup Vendor", "checkout.pickup.far.vendor@test.com", "01000000241", UserRole.Vendor);

        var vendor = new Vendor(

            vendorUser.Id,

            "متجر بعيد",

            "Far Pickup Store",

            "Groceries",

            "1234567855",

            "checkout.pickup.far.vendor@test.com",

            "01000000241",

            city: "Khobar");

        vendor.Approve(10m, Guid.NewGuid());



        var branch = new VendorBranch(

            vendor.Id,

            "Khobar Branch",

            "BR-PICKUP-FAR",

            isPrimary: true,

            "Corniche Road",

            "EASTERN",

            "KHOBAR",

            latitude: 26.22m,

            longitude: 50.19m,

            contactPhone: "01000000242",

            managerName: "Branch Manager",

            managerContact: "01000000243",

            deliveryRadiusKm: 100m);

        var address = new CustomerAddress(

            customer.Id,

            "Far Pickup Customer",

            "01000000240",

            "Olaya",

            AddressLabel.Home,

            city: "Riyadh",

            latitude: 24.71m,

            longitude: 46.67m);

        var cart = new Cart(customer.Id);

        dbContext.Users.AddRange(customer, vendorUser);

        dbContext.Vendors.Add(vendor);

        dbContext.VendorBranches.Add(branch);

        dbContext.CustomerAddresses.Add(address);

        dbContext.Carts.Add(cart);

        await dbContext.SaveChangesAsync();



        var assessment = await CheckoutSupport.EvaluatePickupAsync(

            dbContext,

            cart,

            vendor.Id,

            pickupBranchId: null,

            address,

            CancellationToken.None);



        assessment.BranchId.Should().BeNull();

        assessment.DeliveryCheck.Status.Should().Be("service_area_unavailable");

        assessment.DeliveryCheck.CanProceedToCheckout.Should().BeFalse();

    }



    private static async Task SeedEasternOperationalGeographyAsync(ApplicationDbContext dbContext)

    {

        if (await dbContext.SaudiRegions.AnyAsync())

        {

            return;

        }



        var easternRegionId = Guid.NewGuid();

        var easternRegion = new SaudiRegion(

            easternRegionId,

            "EASTERN",

            "المنطقة الشرقية",

            "Eastern Region",

            26.3927,

            49.9777,

            10,

            1);

        easternRegion.SetOperational(true);



        var cities = new[]

        {

            new SaudiCity(Guid.NewGuid(), easternRegionId, "DAMMAM", "الدمام", "Dammam", 26.3927, 49.9777, 12, 1),

            new SaudiCity(Guid.NewGuid(), easternRegionId, "KHOBAR", "الخبر", "Khobar", 26.2172, 50.1971, 12, 2),

            new SaudiCity(Guid.NewGuid(), easternRegionId, "DHAHRAN", "الظهران", "Dhahran", 26.2361, 50.0393, 12, 3)

        };

        foreach (var city in cities)
        {
            typeof(SaudiCity).GetProperty(nameof(SaudiCity.Region))!
                .SetValue(city, easternRegion);
        }

        dbContext.SaudiRegions.Add(easternRegion);

        dbContext.SaudiCities.AddRange(cities);

        await dbContext.SaveChangesAsync();

    }



    private static ApplicationDbContext CreateDbContext()

    {

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()

            .UseInMemoryDatabase(Guid.NewGuid().ToString())

            .Options;



        return new ApplicationDbContext(options, new AuditableEntityInterceptor());

    }

}


