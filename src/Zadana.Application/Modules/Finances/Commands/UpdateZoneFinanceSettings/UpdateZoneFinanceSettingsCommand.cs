using FluentValidation;
using MediatR;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Commands.UpdateZoneFinanceSettings;

public record UpdateZoneFinanceSettingsCommand(
    Guid ZoneId,
    decimal VatPercent,
    string CodFeeType,
    decimal CodFlatFee,
    decimal CodPercent,
    bool IsVatActive,
    bool IsCodFeeActive) : IRequest<ZoneFinanceSettingsDto>;

public sealed class UpdateZoneFinanceSettingsCommandValidator : AbstractValidator<UpdateZoneFinanceSettingsCommand>
{
    public UpdateZoneFinanceSettingsCommandValidator()
    {
        RuleFor(item => item.ZoneId).NotEmpty();
        RuleFor(item => item.VatPercent).InclusiveBetween(0, 100);
        RuleFor(item => item.CodFlatFee).GreaterThanOrEqualTo(0);
        RuleFor(item => item.CodPercent).InclusiveBetween(0, 100);
        RuleFor(item => item.CodFeeType).NotEmpty().Must(item => item is "flat" or "percent");
    }
}
