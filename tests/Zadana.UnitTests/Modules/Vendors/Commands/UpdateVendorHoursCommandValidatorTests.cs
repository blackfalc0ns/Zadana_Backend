using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorHours;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorHours;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class UpdateVendorHoursCommandValidatorTests
{
    [Fact]
    public void VendorValidator_ShouldAcceptValidClockTimes()
    {
        var validator = new UpdateVendorHoursCommandValidator(CreateLocalizer());
        var command = new UpdateVendorHoursCommand([
            new UpdateVendorHoursItem(0, "00:00", "23:59", true),
            new UpdateVendorHoursItem(1, "09:30", "18:45", true)
        ]);

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("9:00")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("09:00 AM")]
    [InlineData("٠٩:٠٠")]
    public void VendorValidator_ShouldRejectInvalidClockTimes(string value)
    {
        var validator = new UpdateVendorHoursCommandValidator(CreateLocalizer());
        var command = new UpdateVendorHoursCommand([
            new UpdateVendorHoursItem(0, value, "18:00", true)
        ]);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName.EndsWith(".OpenTime"));
    }

    [Theory]
    [InlineData("9:00")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("09:00 AM")]
    [InlineData("٠٩:٠٠")]
    public void AdminValidator_ShouldRejectInvalidClockTimes(string value)
    {
        var validator = new AdminUpdateVendorHoursCommandValidator();
        var command = new AdminUpdateVendorHoursCommand(Guid.NewGuid(), [
            new AdminUpdateVendorHoursItem(0, "09:00", value, true)
        ]);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName.EndsWith(".CloseTime"));
    }

    [Fact]
    public void Parser_ShouldThrowBadRequestException_ForInvalidClockTime()
    {
        var act = () => VendorOperatingHourTimeParser.ParseClockTime("24:00");

        act.Should()
            .Throw<BadRequestException>()
            .Which.ErrorCode.Should().Be("INVALID_OPERATING_HOUR_TIME");
    }

    [Fact]
    public void PrimaryBranchFactory_ShouldKeepGeneratedCodeWithinConfiguredLimit()
    {
        var vendor = new Vendor(
            Guid.NewGuid(),
            new string('A', 80),
            "Store",
            "Retail",
            "CR123",
            "vendor@test.com",
            "1234567890");

        var branch = VendorPrimaryBranchFactory.CreateForHoursProfile(vendor);

        branch.Code.Should().HaveLength(50);
        branch.Name.Should().HaveLength(80);
        branch.IsPrimary.Should().BeTrue();
    }

    private static IStringLocalizer<SharedResource> CreateLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<SharedResource>>();
        localizer.Setup(item => item[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        return localizer.Object;
    }
}
