using MediatR;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Queries.GetCityDeliveryPricingSettings;

public record GetCityDeliveryPricingSettingsQuery : IRequest<List<CityDeliveryPricingSettingsDto>>;
