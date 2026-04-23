using FluentAssertions;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverOperationalStatusFactoryTests
{
    [Fact]
    public void Create_WhenDriverHasAllDocumentsButNotReviewedYet_ShouldReturnUnderReviewGate()
    {
        var driver = CreateDriver();

        var result = DriverOperationalStatusFactory.Create(driver);

        result.GateStatus.Should().Be("UnderReview");
        result.IsOperational.Should().BeFalse();
        result.CanGoAvailable.Should().BeFalse();
        result.Message.Should().Contain("under admin review");
    }

    [Fact]
    public void Create_WhenDriverIsRejected_ShouldReturnRejectedGate()
    {
        var driver = CreateDriver();
        driver.Reject(Guid.NewGuid(), "Missing compliance requirement");

        var result = DriverOperationalStatusFactory.Create(driver);

        result.GateStatus.Should().Be("Rejected");
        result.AccountStatus.Should().Be("Inactive");
        result.IsOperational.Should().BeFalse();
        result.ReviewNote.Should().Be("Missing compliance requirement");
    }

    [Fact]
    public void Create_WhenDriverIsSuspended_ShouldReturnSuspendedGate()
    {
        var driver = CreateDriver();
        driver.Approve(Guid.NewGuid(), "Approved");
        driver.Suspend("Policy violation");

        var result = DriverOperationalStatusFactory.Create(driver);

        result.GateStatus.Should().Be("Suspended");
        result.AccountStatus.Should().Be("Suspended");
        result.IsOperational.Should().BeFalse();
        result.SuspensionReason.Should().Be("Policy violation");
    }

    [Fact]
    public void Create_WhenDriverIsApprovedAndActive_ShouldReturnOperationalGate()
    {
        var driver = CreateDriver();
        driver.Approve(Guid.NewGuid(), "Approved");

        var result = DriverOperationalStatusFactory.Create(driver);

        result.GateStatus.Should().Be("Operational");
        result.IsOperational.Should().BeTrue();
        result.CanReceiveOrders.Should().BeTrue();
        result.CanGoAvailable.Should().BeTrue();
    }

    private static Driver CreateDriver() =>
        new(
            Guid.NewGuid(),
            DriverVehicleType.Motorcycle,
            "29801011234567",
            "CAI-DRV-4421",
            "Nasr City, Cairo",
            "https://cdn.example.com/drivers/id.jpg",
            "https://cdn.example.com/drivers/license.jpg",
            "https://cdn.example.com/drivers/vehicle.jpg",
            "https://cdn.example.com/drivers/photo.jpg");
}
