using MediatR;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Queries.GetRegionDeliveryPricingSettings;

public record GetRegionDeliveryPricingSettingsQuery : IRequest<List<RegionDeliveryPricingSettingsDto>>;
