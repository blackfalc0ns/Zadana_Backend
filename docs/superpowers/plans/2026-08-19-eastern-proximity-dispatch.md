# Eastern Metro Proximity Dispatch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Treat Eastern Region (Dammam / Dhahran / Khobar) as one courier metro: drivers pick the region only (no city), checkout picks the closest branch to the customer, and dispatch offers the closest available driver to that branch using stored GPS — not city/region string locks.

**Architecture:** Keep existing Leaflet + OpenStreetMap tiles for maps. Do **not** call Nominatim or OSRM during checkout/dispatch. Matching uses lat/lng already stored on `CustomerAddress`, `VendorBranch`, and `DriverLocation`, with the same Haversine-style distance already used in `CheckoutSupport` and `DeliveryDispatchScoring`. Assignment ranks by distance, then **drops anyone farther than 50 km**: closest in-range branch to the customer, then closest in-range eligible driver to that branch. `DeliveryProximityLimits.MaxMatchKm = 50`. Do not use city/region strings as the lock.

**Tech Stack:** .NET / EF Core (`Zadana.Application`, `Zadana.Infrastructure`, `Zadana.Api`), xUnit + FluentAssertions, Angular super panel, driver-app contract markdown (driver app is not in this workspace).

## Global Constraints

- Operational signup for drivers stays `EASTERN` only (`OperationalGeographyScope.EasternRegionCode = "EASTERN"`).
- Do not lock customer addresses to Eastern Region.
- Do not require or send a driver `city` on new register/profile flows; `city` remains nullable on `Driver` for old rows.
- Dammam, Dhahran, and Khobar are one metro for courier coverage. Do not filter offers by `DAMMAM` vs `KHOBAR` vs `DHAHRAN`.
- Match radius is **exactly 50 km** (`DeliveryProximityLimits.MaxMatchKm`). Straight-line Haversine, not road distance. Not 30. Not unlimited.
- Branch pick: nearest branch to the customer by coordinates **if distance <= 50 km**. Ignore `VendorBranch.DeliveryRadiusKm` for this matching path. If no branch is within 50 km, the vendor is unavailable for that address.
- Driver pick: nearest eligible driver to the **pickup branch** **if distance <= 50 km**. Eligible also means approved/available/not busy. Fresh GPS farther than 50 km is excluded. Missing or stale GPS is excluded when pickup coordinates exist (cannot prove they are inside 50 km).
- Hard city mismatch (`pickup-customer-city-mismatch`) must stop blocking dispatch when coordinates exist.
- GPS freshness stays `5` minutes (`DeliveryDispatchScoring.GpsFreshnessThreshold`).
- Arabic copy for driver region field: `المنطقة الشرقية` (Dammam / Dhahran / Khobar grouped; no city dropdown).
- Do not add OSM API keys, Nominatim, or OSRM HTTP calls in this work.
- Vendor/customer city pickers stay as they are (out of scope). Driver UI and driver APIs drop cities.
- Frequent commits; TDD; no new markdown except the driver contract update in Task 8.

## File Structure

| File | Responsibility |
| --- | --- |
| Create `src/Zadana.Application/Modules/Geography/Support/GeoDistance.cs` | Shared Haversine km; usable-coordinate checks |
| Create `src/Zadana.Application/Modules/Delivery/Support/DeliveryProximityLimits.cs` | `MaxMatchKm = 50` |
| Create `src/Zadana.Application/Modules/Orders/Support/NearestBranchSelector.cs` | Rank vendor branches by distance; drop > 50 km |
| Modify `src/Zadana.Application/Modules/Geography/Support/OperationalGeographyScope.cs` | Driver service area = Eastern region + any active Eastern vendor, no city |
| Modify `src/Zadana.Domain/Modules/Delivery/Entities/Driver.cs` | `HasServiceArea` = region only |
| Modify `src/Zadana.Application/Modules/Delivery/DTOs/DriverProfileReadinessFactory.cs` | Missing requirement `missing_region` instead of requiring city |
| Modify register/profile/API geography endpoints listed in Task 3–4 | Stop requiring city |
| Modify `CheckoutSupport.cs` and `CartBranchSelectionSupport.cs` | Call `NearestBranchSelector` (distance first, not same-city first) |
| Modify `DeliveryPickupAreaMatcher.cs` and `DeliveryDispatchService.cs` / `DeliveryDispatchScoring.cs` | GPS proximity to pickup |
| Modify `DRIVER_APP_CONTRACTS/OPERATIONAL_GEOGRAPHY_HANDOFF_AR.md` | Mobile: hide cities |
| Modify `zadan_super_panel` driver verification/list city UI | Region only |

---

### Task 1: Shared GeoDistance

**Files:**
- Create: `src/Zadana.Application/Modules/Geography/Support/GeoDistance.cs`
- Create: `tests/Zadana.Application.Tests/Application/Geography/GeoDistanceTests.cs`

**Interfaces:**
- Consumes: none
- Produces: `GeoDistance.HasUsableCoordinates(decimal? latitude, decimal? longitude)`, `GeoDistance.Kilometers(decimal lat1, decimal lng1, decimal lat2, decimal lng2)`, `GeoDistance.TryKilometers(...)`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Zadana.Application.Modules.Geography.Support;

namespace Zadana.Application.Tests.Application.Geography;

public class GeoDistanceTests
{
    [Fact]
    public void Kilometers_DammamToKhobar_ShouldBeUnderThirty()
    {
        // Dammam ~26.3927,49.9777 ; Khobar ~26.2172,50.1971
        var km = GeoDistance.Kilometers(26.3927m, 49.9777m, 26.2172m, 50.1971m);
        km.Should().BeGreaterThan(20m);
        km.Should().BeLessThan(30m);
    }

    [Fact]
    public void HasUsableCoordinates_WhenEitherMissing_ShouldBeFalse()
    {
        GeoDistance.HasUsableCoordinates(null, 50m).Should().BeFalse();
        GeoDistance.HasUsableCoordinates(26m, null).Should().BeFalse();
        GeoDistance.HasUsableCoordinates(26.4m, 50.0m).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~GeoDistanceTests -v n`

Expected: FAIL with `GeoDistance` type not found.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace Zadana.Application.Modules.Geography.Support;

public static class GeoDistance
{
    private const double EarthRadiusKm = 6371d;

    public static bool HasUsableCoordinates(decimal? latitude, decimal? longitude) =>
        latitude.HasValue && longitude.HasValue;

    public static decimal Kilometers(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        var dLat = (double)(lat2 - lat1) * Math.PI / 180;
        var dLng = (double)(lng2 - lng1) * Math.PI / 180;
        var avgLat = (double)(lat1 + lat2) / 2 * Math.PI / 180;
        var x = dLng * Math.Cos(avgLat);
        var y = dLat;
        return (decimal)(Math.Sqrt(x * x + y * y) * EarthRadiusKm);
    }

    public static bool TryKilometers(
        decimal? lat1,
        decimal? lng1,
        decimal? lat2,
        decimal? lng2,
        out decimal kilometers)
    {
        kilometers = 0m;
        if (!HasUsableCoordinates(lat1, lng1) || !HasUsableCoordinates(lat2, lng2))
        {
            return false;
        }

        kilometers = Kilometers(lat1!.Value, lng1!.Value, lat2!.Value, lng2!.Value);
        return true;
    }
}
```

Use the same equirectangular formula as `DeliveryDispatchScoring.ApproximateDistanceKm` so dispatch scores stay consistent with checkout.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~GeoDistanceTests -v n`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Zadana.Application/Modules/Geography/Support/GeoDistance.cs tests/Zadana.Application.Tests/Application/Geography/GeoDistanceTests.cs
git commit -m "feat: add shared geo distance helper for proximity matching"
```

---

### Task 2: Driver service area is Eastern region only

**Files:**
- Modify: `src/Zadana.Application/Modules/Geography/Support/OperationalGeographyScope.cs`
- Modify: `src/Zadana.Domain/Modules/Delivery/Entities/Driver.cs` (`HasServiceArea`)
- Modify: `src/Zadana.Application/Modules/Delivery/DTOs/DriverProfileReadinessFactory.cs`
- Modify: `tests/Zadana.Application.Tests/Application/Orders/DriverRegistrationZoneSelectionTests.cs` (class `DriverRegistrationRegionCityTests`)
- Modify: `tests/Zadana.Application.Tests/Application/Orders/DriverReadServiceTests.cs` (missing city assertions)

**Interfaces:**
- Consumes: `GeoDistance` not required here
- Produces: `OperationalGeographyScope.EnsureDriverServiceAreaAsync(IApplicationDbContext context, string? regionCode, CancellationToken cancellationToken)` — city argument removed. Vendors/branches still use `EnsureOperationalRegionCityAsync` with city.

- [ ] **Step 1: Write the failing tests**

Replace city-required registration tests with region-only behavior. Add this test to `DriverRegistrationRegionCityTests`:

```csharp
[Fact]
public async Task Handle_WhenOnlyEasternRegionProvided_ShouldStartPending()
{
    await using var dbContext = CreateDbContext();
    var operationalRegion = new Domain.Modules.Geography.Entities.SaudiRegion(
        Guid.NewGuid(), "EASTERN", "المنطقة الشرقية", "Eastern Region", 26.4, 50.0, 6, 1);
    dbContext.SaudiRegions.Add(operationalRegion);
    await dbContext.SaveChangesAsync();
    var operationalCity = new Domain.Modules.Geography.Entities.SaudiCity(
        Guid.NewGuid(), operationalRegion.Id, "KHOBAR", "الخبر", "Al Khobar", 26.2, 50.2, 10, 1);
    dbContext.SaudiCities.Add(operationalCity);
    SeedActiveVendorInCity(dbContext, "EASTERN", "KHOBAR");
    await dbContext.SaveChangesAsync();

    var pending = new PendingRegistrationSnapshot(
        Guid.NewGuid(), "Ahmed Driver", "ahmed.driver@example.com", "+201001112233", UserRole.Driver, null);
    var pendingRegistrationService = new Mock<IPendingRegistrationService>();
    pendingRegistrationService
        .Setup(service => service.StartAsync(It.IsAny<StartPendingRegistrationRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PendingRegistrationStartResult(
            PendingRegistrationStartStatus.Succeeded, pending, "1234", "reg-token"));
    var registrationWorkflow = new Mock<IRegistrationWorkflow>();
    registrationWorkflow
        .Setup(workflow => workflow.BuildPendingAuthResponse(pending, "reg-token", null))
        .Returns(new AuthResponseDto(null, null, IsVerified: false, RegistrationToken: "reg-token"));

    var handler = new RegisterDriverCommandHandler(
        pendingRegistrationService.Object,
        registrationWorkflow.Object,
        dbContext,
        Mock.Of<IOtpService>(),
        CreateLocalizer().Object);

    var result = await handler.Handle(CreateCommand(region: "EASTERN", city: null), CancellationToken.None);

    result.RegistrationToken.Should().Be("reg-token");
    pendingRegistrationService.Verify(
        service => service.StartAsync(
            It.Is<StartPendingRegistrationRequest>(request =>
                request.PayloadJson.Contains("EASTERN") && !request.PayloadJson.Contains("DAMMAM")),
            It.IsAny<CancellationToken>()),
        Times.Once);
}
```

Change `RegisterDriverCommandValidator_ShouldRequireCity` to assert city is **optional** (`result.IsValid.Should().BeTrue()` when city is empty and region is `EASTERN` after validator update).

Add a unit-style assertion file `tests/Zadana.Application.Tests/Application/Geography/OperationalGeographyScopeDriverTests.cs`:

```csharp
[Fact]
public async Task EnsureDriverServiceAreaAsync_WhenEasternAndVendorInKhobar_ShouldAllowDammamOmittedCity()
{
    // seed EASTERN + KHOBAR vendor, call EnsureDriverServiceAreaAsync(context, "EASTERN", ct)
    // should not throw
}
```

Update `DriverReadServiceTests` that expect `missing_region_city` when city is empty but region is `EASTERN`: those must expect **no** missing geography requirement.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~DriverRegistrationRegionCityTests|FullyQualifiedName~OperationalGeographyScopeDriverTests -v n`

Expected: FAIL on city required / `EnsureDriverServiceAreaAsync` signature still has city.

- [ ] **Step 3: Write minimal implementation**

In `OperationalGeographyScope.cs` replace `EnsureDriverServiceAreaAsync` with:

```csharp
public static async Task EnsureDriverServiceAreaAsync(
    IApplicationDbContext context,
    string? regionCode,
    CancellationToken cancellationToken)
{
    var normalizedRegion = NormalizeCode(regionCode);
    if (normalizedRegion.Length == 0)
    {
        throw new BusinessRuleException(
            "SERVICE_REGION_REQUIRED",
            "لازم تختار منطقة التشغيل.");
    }

    if (normalizedRegion != EasternRegionCode)
    {
        throw new BusinessRuleException(
            "UNSUPPORTED_OPERATIONAL_REGION",
            "حاليًا التشغيل متاح في المنطقة الشرقية بس.");
    }

    var hasActiveVendor = await context.VendorBranches
        .AsNoTracking()
        .AnyAsync(
            branch =>
                branch.IsActive
                && branch.Vendor.Status == VendorStatus.Active
                && branch.Vendor.AcceptOrders
                && branch.Vendor.LockedAtUtc == null
                && (
                    branch.Region == EasternRegionCode
                    || branch.Vendor.Region == EasternRegionCode
                    || branch.City == "DAMMAM"
                    || branch.City == "KHOBAR"
                    || branch.City == "DHAHRAN"
                    || branch.City == "الدمام"
                    || branch.City == "الخبر"
                    || branch.City == "الظهران"
                    || branch.City == "Dammam"
                    || branch.City == "Al Khobar"
                    || branch.City == "Dhahran"),
            cancellationToken);

    if (!hasActiveVendor)
    {
        throw new BusinessRuleException(
            "DRIVER_REGION_HAS_NO_ACTIVE_VENDOR",
            "المنطقة الشرقية ما فيها متاجر متاحة حاليًا.");
    }
}
```

Keep `EnsureOperationalRegionCityAsync` unchanged for vendors/branches.

In `Driver.cs`:

```csharp
public bool HasServiceArea => !string.IsNullOrWhiteSpace(Region);
```

In `DriverProfileReadinessFactory.GetMissingRequirements`:

```csharp
if (string.IsNullOrWhiteSpace(region))
{
    missing.Add("missing_region");
}
```

Replace every `missing_region_city` checklist reference in that file with `missing_region`.

Update `RegisterDriverCommandValidator` so `City` is optional (`MaximumLength(50)` only). `Region` stays required.

Update every caller of `EnsureDriverServiceAreaAsync` to drop the city argument (Task 3 lists them; do the method signature in this task and fix compile errors in the same commit).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~DriverRegistration|FullyQualifiedName~OperationalGeographyScopeDriver|FullyQualifiedName~DriverReadServiceTests -v n`

Expected: PASS (fix any leftover `missing_region_city` asserts).

- [ ] **Step 5: Commit**

```bash
git add src/Zadana.Application/Modules/Geography/Support/OperationalGeographyScope.cs src/Zadana.Domain/Modules/Delivery/Entities/Driver.cs src/Zadana.Application/Modules/Delivery/DTOs/DriverProfileReadinessFactory.cs tests/Zadana.Application.Tests
git commit -m "feat: make driver service area Eastern region only"
```

---

### Task 3: Register, profile, and geography APIs drop driver city

**Files:**
- Modify: `src/Zadana.Application/Modules/Delivery/Commands/RegisterDriver/RegisterDriverCommand.cs` (keep `City` nullable for old clients)
- Modify: `src/Zadana.Application/Modules/Delivery/Commands/RegisterDriver/RegisterDriverCommandHandler.cs`
- Modify: `src/Zadana.Api/Modules/Delivery/Requests/RegisterDriverRequest.cs` — remove `[Required]` on `City`
- Modify: `src/Zadana.Application/Modules/Delivery/Commands/UpdateDriverProfile/UpdateDriverProfileCommand.cs`
- Modify: `src/Zadana.Api/Modules/Delivery/Controllers/DriverProfileController.cs` (`PUT vehicle`)
- Modify: `src/Zadana.Api/Modules/Geography/Controllers/GeographyController.cs`
- Modify: any admin driver update that still calls `EnsureDriverServiceAreaAsync` with city

**Interfaces:**
- Consumes: `EnsureDriverServiceAreaAsync(context, region, ct)` from Task 2
- Produces: `GET /api/geography/driver/regions` returns only Eastern. `GET /api/geography/driver/regions/{regionCode}/cities` returns `[]`. Register/profile persist `City = null` when omitted.

- [ ] **Step 1: Write the failing API-oriented tests**

Add `tests/Zadana.Application.Tests/Application/Geography/GeographyDriverRegionsTests.cs` only if you can hit the controller via existing web factory. Prefer handler-level: after register, pending payload region is `EASTERN` and city is null.

In `DriverRegistrationRegionCityTests`, assert `CreateCommand` with `city: "DAMMAM"` still succeeds (backward compatible) but dispatch later ignores city (Task 6).

Add test that `GET driver cities` returns empty — if no controller test harness exists, add a small test that documents the query used by `GetDriverCities` by extracting the empty-list contract into `OperationalGeographyScope.DriverCitySelectionEnabled = false` and asserting:

```csharp
OperationalGeographyScope.DriverShowsCityPicker.Should().BeFalse();
```

Add that flag:

```csharp
public const bool DriverShowsCityPicker = false;
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~DriverShowsCityPicker|FullyQualifiedName~DriverRegistrationRegionCityTests -v n`

Expected: FAIL on missing flag / still requiring city in profile handler.

- [ ] **Step 3: Write minimal implementation**

`RegisterDriverCommandHandler`:

```csharp
if (string.IsNullOrWhiteSpace(request.Region))
{
    throw new BusinessRuleException(
        "DRIVER_SERVICE_AREA_REQUIRED",
        "لازم تختار منطقة التشغيل للمندوب.");
}

await OperationalGeographyScope.EnsureDriverServiceAreaAsync(
    _context,
    request.Region,
    cancellationToken);
```

Serialize `request.City` as-is (may be null). Do not default city to `DAMMAM`.

`UpdateDriverProfileCommandHandler` and `DriverProfileController.UpdateVehicle`: require region only; call `EnsureDriverServiceAreaAsync` without city; `driver.UpdateServiceArea(request.Region, request.City)` with null city allowed.

`RegisterDriverRequest`: `string? City` without `[Required]`.

`GeographyController`:

```csharp
[HttpGet("driver/regions")]
public Task<IReadOnlyList<SaudiRegionLookupDto>> GetDriverRegions(CancellationToken cancellationToken)
{
    return _dbContext.SaudiRegions
        .AsNoTracking()
        .Where(region => region.Code == OperationalGeographyScope.EasternRegionCode)
        .OrderBy(region => region.SortOrder)
        .Select(region => new SaudiRegionLookupDto(
            region.Code,
            region.NameAr,
            region.NameEn,
            region.Latitude,
            region.Longitude,
            region.MapZoom,
            region.SortOrder))
        .ToListAsync(cancellationToken);
}

[HttpGet("driver/regions/{regionCode}/cities")]
public Task<IReadOnlyList<SaudiCityLookupDto>> GetDriverCities(string regionCode, CancellationToken cancellationToken)
{
    return Task.FromResult<IReadOnlyList<SaudiCityLookupDto>>([]);
}
```

Keep `GET /api/geography/regions/{regionCode}/cities` for vendors/customers.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~RegisterDriver|FullyQualifiedName~UpdateDriverProfile|FullyQualifiedName~DriverRegistration -v n`

Expected: PASS. Also `dotnet build src/Zadana.Api/Zadana.Api.csproj` Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Zadana.Application/Modules/Delivery src/Zadana.Api/Modules/Delivery src/Zadana.Api/Modules/Geography tests/Zadana.Application.Tests
git commit -m "feat: drop required driver city from register and profile APIs"
```

---

### Task 4: Nearest branch to the customer

**Files:**
- Create: `src/Zadana.Application/Modules/Delivery/Support/DeliveryProximityLimits.cs`
- Create: `src/Zadana.Application/Modules/Orders/Support/NearestBranchSelector.cs`
- Create: `tests/Zadana.Application.Tests/Application/Orders/NearestBranchSelectorTests.cs`
- Modify: `src/Zadana.Application/Modules/Checkout/Support/CheckoutSupport.cs` (`ResolvePickupBranchForPricing` / `OrderBranchesForAddress`)
- Modify: `src/Zadana.Application/Modules/Orders/Support/CartBranchSelectionSupport.cs`

**Interfaces:**
- Consumes: `GeoDistance.Kilometers`, `GeoDistance.HasUsableCoordinates`, `DeliveryProximityLimits.MaxMatchKm` (`50`)
- Produces: `NearestBranchSelector.Order<T>(...)` returning branches **within 50 km**, sorted by distance (then primary, then created). A Riyadh branch for a Khobar customer is dropped.

Current bug relative to the spec: `CheckoutSupport.OrderBranchesForAddress` and `CartBranchSelectionSupport.ResolveBestBranchForAddress` **prefer same city first**, and only then nearest GPS. They also use `DeliveryRadiusKm` instead of the platform 50 km rule.

Also create `src/Zadana.Application/Modules/Delivery/Support/DeliveryProximityLimits.cs`:

```csharp
namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryProximityLimits
{
    public const decimal MaxMatchKm = 50m;
}
```

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Orders.Support;

namespace Zadana.Application.Tests.Application.Orders;

public class NearestBranchSelectorTests
{
    [Fact]
    public void MaxMatchKm_ShouldBeFifty()
    {
        DeliveryProximityLimits.MaxMatchKm.Should().Be(50m);
    }

    [Fact]
    public void Order_ShouldPickCloserKhobarBranchOverFartherDammamBranch()
    {
        var dammam = new FakeBranch(Guid.NewGuid(), 26.43m, 50.08m, isPrimary: true);
        var khobar = new FakeBranch(Guid.NewGuid(), 26.22m, 50.19m, isPrimary: false);
        var ordered = NearestBranchSelector.Order(
            [dammam, khobar],
            customerLatitude: 26.2172m,
            customerLongitude: 50.1971m,
            lat: b => b.Latitude,
            lng: b => b.Longitude,
            isPrimary: b => b.IsPrimary,
            createdAt: b => b.CreatedAtUtc).ToList();

        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(khobar.Id);
    }

    [Fact]
    public void Order_ShouldDropBranchFartherThanFiftyKm()
    {
        var khobar = new FakeBranch(Guid.NewGuid(), 26.22m, 50.19m, isPrimary: false);
        var riyadh = new FakeBranch(Guid.NewGuid(), 24.71m, 46.67m, isPrimary: true);
        var ordered = NearestBranchSelector.Order(
            [khobar, riyadh],
            26.2172m,
            50.1971m,
            b => b.Latitude,
            b => b.Longitude,
            b => b.IsPrimary,
            b => b.CreatedAtUtc).ToList();

        ordered.Should().ContainSingle().Which.Id.Should().Be(khobar.Id);
    }

    private sealed record FakeBranch(
        Guid Id,
        decimal Latitude,
        decimal Longitude,
        bool IsPrimary,
        DateTime CreatedAtUtc = default);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~NearestBranchSelectorTests -v n`

Expected: FAIL `NearestBranchSelector` not found.

- [ ] **Step 3: Write minimal implementation**

```csharp
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Geography.Support;

namespace Zadana.Application.Modules.Orders.Support;

public static class NearestBranchSelector
{
    public static IEnumerable<T> Order<T>(
        IReadOnlyCollection<T> branches,
        decimal? customerLatitude,
        decimal? customerLongitude,
        Func<T, decimal?> latitude,
        Func<T, decimal?> longitude,
        Func<T, bool> isPrimary,
        Func<T, DateTime> createdAtUtc)
    {
        if (branches.Count == 0)
        {
            return [];
        }

        if (!GeoDistance.HasUsableCoordinates(customerLatitude, customerLongitude))
        {
            return branches
                .OrderByDescending(isPrimary)
                .ThenBy(createdAtUtc);
        }

        return branches
            .Where(branch => GeoDistance.HasUsableCoordinates(latitude(branch), longitude(branch)))
            .Select(branch =>
            {
                var distanceKm = GeoDistance.Kilometers(
                    latitude(branch)!.Value,
                    longitude(branch)!.Value,
                    customerLatitude!.Value,
                    customerLongitude!.Value);
                return (Branch: branch, DistanceKm: distanceKm);
            })
            .Where(item => item.DistanceKm <= DeliveryProximityLimits.MaxMatchKm)
            .OrderBy(item => item.DistanceKm)
            .ThenByDescending(item => isPrimary(item.Branch))
            .ThenBy(item => createdAtUtc(item.Branch))
            .Select(item => item.Branch);
    }
}
```

Do not use `VendorBranch.DeliveryRadiusKm` on this path. The only cutoff is `MaxMatchKm = 50`.

Wire `CheckoutSupport.OrderBranchesForAddress` to:

```csharp
return NearestBranchSelector.Order(
    branches,
    address.Latitude,
    address.Longitude,
    branch => branch.Latitude,
    branch => branch.Longitude,
    branch => branch.IsPrimary,
    branch => branch.CreatedAtUtc);
```

Remove the `sameCityBranches` early return. If the customer has coordinates and no branch is within 50 km, return empty (unavailable). If the customer has **no** coordinates, keep primary/created fallback.

Same change in `CartBranchSelectionSupport.ResolveBestBranchForAddress` (`FirstOrDefault()` of selector).

Same change in `CheckoutSupport` pickup-branch resolver around the `sameCityBranch` block (lines ~258–267): delete same-city preference; use selector.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~NearestBranchSelectorTests|FullyQualifiedName~CheckoutSupport -v n`

Expected: PASS. Also run any cart/checkout tests: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~Cart -v n`

- [ ] **Step 5: Commit**

```bash
git add src/Zadana.Application/Modules/Delivery/Support/DeliveryProximityLimits.cs src/Zadana.Application/Modules/Orders/Support/NearestBranchSelector.cs src/Zadana.Application/Modules/Checkout/Support/CheckoutSupport.cs src/Zadana.Application/Modules/Orders/Support/CartBranchSelectionSupport.cs tests/Zadana.Application.Tests/Application/Orders/NearestBranchSelectorTests.cs
git commit -m "feat: select nearest vendor branch within 50 km"
```

---

### Task 5: Match drivers by 50 km to pickup, not by city

**Files:**
- Modify: `src/Zadana.Application/Modules/Delivery/Support/DeliveryPickupAreaMatcher.cs`
- Modify: `tests/Zadana.Application.Tests/Application/Orders/DeliveryPickupAreaMatcherTests.cs`

**Interfaces:**
- Consumes: `GeoDistance`, `DeliveryProximityLimits.MaxMatchKm`
- Produces:

```csharp
public static bool DriverMatchesPickup(
    decimal? driverLatitude,
    decimal? driverLongitude,
    decimal? pickupLatitude,
    decimal? pickupLongitude,
    bool gpsFresh);
```

City strings are ignored. Fresh GPS and distance `<= 50` is required. Stale or missing GPS is a miss.

- [ ] **Step 1: Write the failing tests (replace old city tests)**

```csharp
public class DeliveryPickupAreaMatcherTests
{
    [Fact]
    public void DriverMatchesPickup_WhenFreshGpsInsideFiftyKm_ShouldBeTrue()
    {
        DeliveryPickupAreaMatcher.DriverMatchesPickup(
            driverLatitude: 26.22m,
            driverLongitude: 50.19m,
            pickupLatitude: 26.39m,
            pickupLongitude: 49.98m,
            gpsFresh: true).Should().BeTrue();
    }

    [Fact]
    public void DriverMatchesPickup_WhenFreshGpsBeyondFiftyKm_ShouldBeFalse()
    {
        DeliveryPickupAreaMatcher.DriverMatchesPickup(
            driverLatitude: 24.71m,
            driverLongitude: 46.68m,
            pickupLatitude: 26.39m,
            pickupLongitude: 49.98m,
            gpsFresh: true).Should().BeFalse();
    }

    [Fact]
    public void DriverMatchesPickup_WhenGpsStale_ShouldBeFalse()
    {
        DeliveryPickupAreaMatcher.DriverMatchesPickup(
            26.22m, 50.19m, 26.39m, 49.98m, gpsFresh: false).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~DeliveryPickupAreaMatcherTests -v n`

Expected: FAIL (old city API / missing `DriverMatchesPickup`).

- [ ] **Step 3: Write minimal implementation**

```csharp
using Zadana.Application.Modules.Geography.Support;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryPickupAreaMatcher
{
    public static bool DriverMatchesPickup(
        decimal? driverLatitude,
        decimal? driverLongitude,
        decimal? pickupLatitude,
        decimal? pickupLongitude,
        bool gpsFresh)
    {
        if (!gpsFresh)
        {
            return false;
        }

        if (!GeoDistance.TryKilometers(
                driverLatitude,
                driverLongitude,
                pickupLatitude,
                pickupLongitude,
                out var km))
        {
            return false;
        }

        return km <= DeliveryProximityLimits.MaxMatchKm;
    }

    public static bool DriverMatchesDeliveryArea(Driver driver, string? storeCity, string? customerCity)
    {
        _ = driver;
        _ = storeCity;
        _ = customerCity;
        return true;
    }

    public static List<Driver> FilterDrivers(
        IEnumerable<Driver> drivers,
        string? storeCity,
        string? customerCity)
    {
        _ = storeCity;
        _ = customerCity;
        return drivers.ToList();
    }
}
```

Keep the 3-argument `FilterDrivers` until Task 6 rewires dispatch to `DriverMatchesPickup`. City overloads must not exclude Khobar vs Dammam.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~DeliveryPickupAreaMatcherTests -v n`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Zadana.Application/Modules/Delivery/Support/DeliveryPickupAreaMatcher.cs tests/Zadana.Application.Tests/Application/Orders/DeliveryPickupAreaMatcherTests.cs
git commit -m "feat: require courier GPS within 50 km of pickup"
```

---

### Task 6: Dispatch offers nearest driver to the branch

**Files:**
- Modify: `src/Zadana.Infrastructure/Modules/Delivery/Services/DeliveryDispatchService.cs` (remove city gates ~638–654; change `SelectDriversForDeliveryArea`)
- Modify: `src/Zadana.Infrastructure/Modules/Delivery/Services/DeliveryDispatchScoring.cs`
- Modify: `tests/Zadana.Application.Tests/Application/Orders/DeliveryDispatchServiceTests.cs`
- Modify: `tests/Zadana.Application.Tests/Application/Orders/DeliveryDispatchScoring` tests if present

**Interfaces:**
- Consumes: `DeliveryPickupAreaMatcher.DriverMatchesPickup`, `DeliveryProximityLimits.MaxMatchKm`, `GeoDistance`
- Produces: dispatch offers the closest driver whose **fresh GPS is within 50 km of the pickup branch**. City labels do not matter. Drivers outside 50 km or without fresh GPS are not offered.

- [ ] **Step 1: Write the failing dispatch tests**

In `DeliveryDispatchServiceTests`, add a scenario: pickup branch in Dammam, customer in Khobar (different city strings), driver A in Khobar 3 km from branch, driver B registered `DAMMAM` but 20 km away. Expect driver A.

Replace city-mismatch behavior: today dispatch returns null with `pickup-customer-city-mismatch`. New test:

```csharp
[Fact]
public async Task TryAutoDispatchAsync_WhenCustomerCityDiffersFromBranchCity_ShouldStillOfferNearestDriver()
{
    await using var dbContext = CreateDbContext();
    var scenario = await SeedDispatchScenarioAsync(
        dbContext,
        sameZoneDriverCity: "KHOBAR",
        customerCity: "الخبر",
        branchCity: "DAMMAM");
    // place SameZoneFreshDriver GPS near the Dammam branch
    var service = CreateDispatchService(dbContext);

    var decision = await service.TryAutoDispatchAsync(scenario.Order.Id, cancellationToken: CancellationToken.None);

    decision.Should().NotBeNull();
    decision!.DriverId.Should().Be(scenario.SameZoneFreshDriver.Id);
}
```

Extend `SeedDispatchScenarioAsync` in that file with `customerCity` and `branchCity` parameters if they do not exist (the helper already has `sameZoneDriverCity`).

Change `TryAutoDispatchAsync_ShouldPreferDriverWithFreshGpsInSameZone` expected `MatchReason` from `"region-city-live-gps"` to `"pickup-live-gps"`.

Add a scoring unit test in `tests/Zadana.Application.Tests/Application/Orders/DeliveryDispatchScoringTests.cs` (create if missing):

```csharp
[Fact]
public void EvaluateCandidate_ShouldPreferCloserDriverOverSameCityLabel()
{
    var now = DateTime.UtcNow;
    var context = new DeliveryDispatchContext(
        PickupZone: null,
        PickupCity: "DAMMAM",
        PickupRegion: "EASTERN",
        PickupLatitude: 26.3927m,
        PickupLongitude: 49.9777m);

    var closeDriver = CreateDriver(region: "EASTERN", city: "KHOBAR");
    var farDriver = CreateDriver(region: "EASTERN", city: "DAMMAM");
    var closeEval = DeliveryDispatchScoring.EvaluateCandidate(
        closeDriver,
        new DriverLocation(closeDriver.Id, 26.40m, 50.00m, now, 10m),
        activeTaskCount: 0,
        reliabilityScore: 50m,
        commitmentScore: 100m,
        context,
        now);
    var farEval = DeliveryDispatchScoring.EvaluateCandidate(
        farDriver,
        new DriverLocation(farDriver.Id, 26.20m, 50.20m, now, 10m),
        0, 50m, 100m, context, now);

    closeEval.CompositeScore.Should().BeLessThan(farEval.CompositeScore);
    closeEval.MatchReason.Should().Be("pickup-live-gps");
}
```

Add also:

```csharp
[Fact]
public async Task TryAutoDispatchAsync_WhenDriverGpsBeyondFiftyKm_ShouldNotOfferThatDriver()
{
    // Seed only a Riyadh-GPS driver against a Dammam pickup. Decision should be null
    // with note Dispatch pending: no-eligible-driver-in-pickup-area
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~DeliveryDispatch -v n`

Expected: FAIL on city mismatch null / old match reason.

- [ ] **Step 3: Write minimal implementation**

In `DeliveryDispatchService.TryAutoDispatchLockedAsync`, **delete** these blocks:

- `Dispatch pending: missing-pickup-city`
- `Dispatch pending: missing-customer-city`
- `Dispatch pending: pickup-customer-city-mismatch`

Replace with: if pickup lat/lng missing, note `Dispatch pending: missing-pickup-coordinates` and return null (cannot rank proximity). Customer city is not required.

Change `SelectDriversForDeliveryArea` to require fresh GPS within 50 km of pickup:

```csharp
private static List<Driver> SelectDriversForDeliveryArea(
    IEnumerable<Driver> drivers,
    IReadOnlyDictionary<Guid, DriverLocation> latestLocations,
    DateTime utcNow,
    decimal? pickupLatitude,
    decimal? pickupLongitude,
    HashSet<Guid> rejectedDriverIds,
    HashSet<Guid> timedOutDriverIds,
    bool includeTimedOutDrivers) =>
    drivers
        .Where(driver =>
            !rejectedDriverIds.Contains(driver.Id) &&
            (includeTimedOutDrivers || !timedOutDriverIds.Contains(driver.Id)))
        .Where(driver =>
        {
            latestLocations.TryGetValue(driver.Id, out var location);
            var gpsFresh = location is not null
                && (utcNow - location.RecordedAtUtc) <= DeliveryDispatchScoring.GpsFreshnessThreshold;
            return DeliveryPickupAreaMatcher.DriverMatchesPickup(
                location?.Latitude,
                location?.Longitude,
                pickupLatitude,
                pickupLongitude,
                gpsFresh);
        })
        .ToList();
```

Load `latestLocations` before this filter. `DriverMatchesDeliveryAreaAsync` must use the same 50 km GPS check against the order's pickup coordinates (not city strings).

Scoring `EvaluateCandidate`: stop using `sameRegionCity` / `sameCity` for tier. Tiers are GPS quality only among drivers already inside 50 km:

```csharp
private static int ResolveTier(bool gpsFresh, bool lowConfidenceGps, bool inPickupZone) =>
    gpsFresh && !lowConfidenceGps && inPickupZone ? 1
    : gpsFresh && !lowConfidenceGps ? 2
    : gpsFresh ? 3
    : 4;

private static string ResolveMatchReason(int tier) =>
    tier switch
    {
        1 => "pickup-live-gps",
        2 => "pickup-live-gps",
        3 => "pickup-low-confidence-gps",
        _ => "no-fresh-gps-fallback"
    };
```

`compositeScore` stays `(tier * 1000m) + (distanceKm * 3m) + ...` so the closer in-range driver wins. Do not use `DeliveryRadiusKm`. The only km rule is `MaxMatchKm = 50`.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj --filter FullyQualifiedName~DeliveryDispatch -v n`

Expected: PASS. Then full: `dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj tests/Zadana.UnitTests/Zadana.UnitTests.csproj -v q`

Fix any unit tests that still assume city lock (`DeliveryPricingServiceTests` city names can stay; pricing is not this feature).

- [ ] **Step 5: Commit**

```bash
git add src/Zadana.Infrastructure/Modules/Delivery/Services/DeliveryDispatchService.cs src/Zadana.Infrastructure/Modules/Delivery/Services/DeliveryDispatchScoring.cs tests/Zadana.Application.Tests/Application/Orders
git commit -m "feat: dispatch nearest courier to pickup branch by GPS"
```

---

### Task 7: Super panel driver geography UI

**Files:**
- Modify: `f:/Zadna/zadan_super_panel/src/app/features/drivers/components/driver-verification-tab/driver-verification-tab.component.ts`
- Modify: matching `.html` template (city select)
- Modify: `f:/Zadna/zadan_super_panel/src/app/features/drivers/pages/list/drivers-list/drivers-list.component.ts` (optional city filter: keep as display filter on stored city, or switch to region-only)
- Modify: `f:/Zadna/zadan_super_panel/src/app/shared/services/geography.service.ts` — add `getDriverRegions()`

**Interfaces:**
- Consumes: `GET /api/geography/driver/regions`
- Produces: edit form sends `{ region: 'EASTERN', city: null }` (or omits city)

- [ ] **Step 1: Write a failing UI contract assertion**

There is no Angular unit test harness required. Add a TypeScript comment-free change and verify by build.

If the project has specs, skip inventing a new Karma test. Verification is `npm` lint/build.

- [ ] **Step 2: Confirm current city dropdown exists**

Open `driver-verification-tab.component.html` and locate `editForm.city` / `loadCities`.

- [ ] **Step 3: Minimal UI change**

- Load `getDriverRegions()` (or hardcode Eastern display names).
- Remove city `<select>` from driver edit form.
- On save, send `region: this.editForm.region || 'EASTERN'` and `city: null`.
- Show helper text: Arabic `المنطقة الشرقية (الدمام - الظهران - الخبر)` / English `Eastern Region (Dammam, Dhahran, Khobar)`.
- List page: city filter may remain for legacy `Driver.City` values; label it as legacy. Do not require city on create/edit.

- [ ] **Step 4: Verify**

Run in `f:/Zadna/zadan_super_panel`: `npx ng build --configuration=development`

Expected: compile success.

Browser: open a driver profile edit tab, confirm no city list, save still works (needs running API — if API is down, build is the gate).

- [ ] **Step 5: Commit** (super panel repo)

```bash
git add src/app/features/drivers src/app/shared/services/geography.service.ts
git commit -m "feat: show Eastern Region only on driver profile, hide cities"
```

---

### Task 8: Driver app contract (mobile is out of repo)

**Files:**
- Modify: `DRIVER_APP_CONTRACTS/OPERATIONAL_GEOGRAPHY_HANDOFF_AR.md`
- Modify: `DRIVER_APP_CONTRACTS/PROFILE_CONTRACT.md` if it still documents `city` as required
- Modify: `DRIVER_APP_CONTRACTS/DOCUMENTS_COMPLIANCE_EXPANSION_HANDOFF_AR.md` missing `missing_region_city` → `missing_region`

**Interfaces:**
- Consumes: Task 3 APIs
- Produces: mobile handoff describing region-only UI

- [ ] **Step 1: Rewrite the operational geography handoff**

Replace city sections with:

- Register and profile: one field `المنطقة الشرقية` covering Dammam / Dhahran / Khobar. No city picker.
- `POST` register body: `{ "region": "EASTERN" }` (`city` optional, ignored for dispatch).
- `PUT /api/drivers/me/profile/vehicle`: `region` required, `city` optional.
- `GET /api/geography/driver/regions` — single Eastern row.
- `GET /api/geography/driver/regions/EASTERN/cities` — empty list; do not render a city dropdown.
- Errors: drop `SERVICE_CITY_REQUIRED` / `DRIVER_CITY_HAS_NO_ACTIVE_VENDOR` / `UNSUPPORTED_OPERATIONAL_CITY` for driver flows. Keep them for vendor/branch city APIs.
- New: `DRIVER_REGION_HAS_NO_ACTIVE_VENDOR`.
- Completion: `missing_region` instead of `missing_region_city`.
- Explicit: offers go to the closest eligible driver to the pickup branch **within 50 km**. No city lock.

- [ ] **Step 2: Read the file and confirm no leftover “أرسل city DAMMAM” as a requirement**

- [ ] **Step 3: Profile contract** — document that vehicle update uses `region` not `primaryZoneId`/`city`.

- [ ] **Step 4: No automated test**

- [ ] **Step 5: Commit**

```bash
git add DRIVER_APP_CONTRACTS/OPERATIONAL_GEOGRAPHY_HANDOFF_AR.md DRIVER_APP_CONTRACTS/PROFILE_CONTRACT.md DRIVER_APP_CONTRACTS/DOCUMENTS_COMPLIANCE_EXPANSION_HANDOFF_AR.md
git commit -m "docs: driver geography is Eastern metro without cities"
```

---

## OpenStreetMap — what is and is not in this plan

**Yes, this is possible with the OpenStreetMap stack you already use:**

| Need | OSM role | This plan |
| --- | --- | --- |
| Show maps in vendor/super panels | Leaflet tiles `© OpenStreetMap` (already in `staff-branches.page.ts`, tracking maps) | Unchanged |
| Nearest branch / nearest driver | **Not** an OSM API. Use stored lat/lng + Haversine (`GeoDistance`) | Implemented |
| Road distance / ETA | OSRM (OSM roads). Vendor panel already fetches OSRM for display | **Out of scope** (YAGNI). Straight-line km is enough for Dammam–Khobar–Dhahran (~25 km metro) |
| Address geocoding | Nominatim | **Out of scope**. Customer/branch coordinates must already exist |

Do not add live Nominatim/OSRM to dispatch: public OSM services rate-limit and would block offer latency.

## Self-review

**Spec coverage:**
- Driver selects Eastern → all nearby orders: Tasks 5–6
- Signup shows Eastern as one metro, hide cities: Tasks 2–3, 7–8
- Change from driver profile: Tasks 3, 7–8
- Closest branch to customer: Task 4
- Closest driver to that branch, not city-locked, **within 50 km**: Tasks 5–6
- OSM feasibility: architecture + OSM section

**Out of scope (do not implement unless a later plan):** vendor/customer city dropdowns, OSRM in dispatch, expanding operations outside Eastern signup, deleting `Driver.City` column. Do not use `VendorBranch.DeliveryRadiusKm` as the match cutoff; use `MaxMatchKm = 50` only.

**Placeholder scan:** none remaining.

**Type consistency:** `EnsureDriverServiceAreaAsync(context, region, ct)`; `missing_region`; `DeliveryProximityLimits.MaxMatchKm = 50`; `DriverMatchesPickup`; match reasons `pickup-live-gps` / `pickup-low-confidence-gps` / `no-fresh-gps-fallback`.

## Verification (after all tasks)

```bash
dotnet test tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj tests/Zadana.UnitTests/Zadana.UnitTests.csproj
dotnet build src/Zadana.Api/Zadana.Api.csproj
```

Manual: place a Khobar customer, Dammam + Khobar branches of the same vendor, confirm checkout uses the closer branch if both are within 50 km; a Khobar driver with fresh GPS gets a Dammam-branch offer; a Riyadh GPS driver does not.
