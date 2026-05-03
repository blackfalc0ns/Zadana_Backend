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
