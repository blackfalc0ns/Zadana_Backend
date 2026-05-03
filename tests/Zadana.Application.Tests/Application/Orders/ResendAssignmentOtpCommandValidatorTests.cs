using FluentAssertions;
using Zadana.Application.Modules.Delivery.Commands.ResendAssignmentOtp;

namespace Zadana.Application.Tests.Application.Orders;

public class ResendAssignmentOtpCommandValidatorTests
{
    [Fact]
    public async Task Validate_WhenOtpTypeIsNull_ShouldReturnValidationError_NotThrow()
    {
        var validator = new ResendAssignmentOtpCommandValidator();
        var command = new ResendAssignmentOtpCommand(Guid.NewGuid(), Guid.NewGuid(), null!);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(ResendAssignmentOtpCommand.OtpType));
    }
}
