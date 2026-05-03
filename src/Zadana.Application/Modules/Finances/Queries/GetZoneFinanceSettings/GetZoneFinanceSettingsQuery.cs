using MediatR;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Queries.GetZoneFinanceSettings;

public record GetZoneFinanceSettingsQuery : IRequest<List<ZoneFinanceSettingsDto>>;
