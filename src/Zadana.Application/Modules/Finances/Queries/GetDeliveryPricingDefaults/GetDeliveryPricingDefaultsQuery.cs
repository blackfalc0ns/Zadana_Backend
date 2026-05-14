using MediatR;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Queries.GetDeliveryPricingDefaults;

public record GetDeliveryPricingDefaultsQuery : IRequest<DeliveryPricingDefaultsDto>;
