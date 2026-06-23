using FluentAssertions;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Application.Tests.Delivery;

public class DeliveryAssignmentOtpTests
{
    [Fact]
    public void EnsurePickupOtp_WhenPreviousCodeExpired_ShouldGenerateNewCode()
    {
        var assignment = new DeliveryAssignment(Guid.NewGuid(), 0m);
        var firstCode = assignment.EnsurePickupOtp(TimeSpan.FromMinutes(5));

        assignment.GetType().GetProperty(nameof(DeliveryAssignment.PickupOtpExpiresAtUtc))!
            .SetValue(assignment, DateTime.UtcNow.AddMinutes(-1));

        var secondCode = assignment.EnsurePickupOtp(TimeSpan.FromMinutes(5));

        secondCode.Should().NotBe(firstCode);
    }

    [Fact]
    public void RegeneratePickupOtp_ShouldInvalidatePreviousCode()
    {
        var driverId = Guid.NewGuid();
        var assignment = new DeliveryAssignment(Guid.NewGuid(), 0m);
        assignment.OfferTo(driverId, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();

        var firstCode = assignment.RegeneratePickupOtp(TimeSpan.FromHours(2));
        var secondCode = assignment.RegeneratePickupOtp(TimeSpan.FromHours(2));

        secondCode.Should().NotBe(firstCode);

        Action act = () => assignment.VerifyPickupOtp(driverId, firstCode);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalid*");
    }
}
