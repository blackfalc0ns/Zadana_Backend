using FluentAssertions;
using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverVehicleTypeMapperTests
{
    [Theory]
    [InlineData("bike", DriverVehicleType.Bicycle)]
    [InlineData("Cargo Van", DriverVehicleType.Van)]
    [InlineData("motorbike", DriverVehicleType.Motorcycle)]
    [InlineData("Motorcycle", DriverVehicleType.Motorcycle)]
    public void TryParse_ShouldSupportLegacyAndCanonicalValues(string rawValue, DriverVehicleType expected)
    {
        var parsed = DriverVehicleTypeMapper.TryParse(rawValue, out var vehicleType);

        parsed.Should().BeTrue();
        vehicleType.Should().Be(expected);
    }

    [Fact]
    public void ToStorageValue_ShouldNormalizeAliasesToCanonicalEnumNames()
    {
        var value = DriverVehicleTypeMapper.ToStorageValue(DriverVehicleType.Motorbike);

        value.Should().Be("Motorcycle");
    }
}
