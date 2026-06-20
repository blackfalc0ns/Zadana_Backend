using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Support;

namespace Zadana.Application.Modules.Identity.Queries.AdminCustomers;

public record GetAdminCustomerFilterOptionsQuery : IRequest<AdminCustomerFilterOptionsDto>;

public class GetAdminCustomerFilterOptionsQueryHandler
    : IRequestHandler<GetAdminCustomerFilterOptionsQuery, AdminCustomerFilterOptionsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IGeographyCityResolver _geographyCityResolver;

    public GetAdminCustomerFilterOptionsQueryHandler(
        IApplicationDbContext context,
        IGeographyCityResolver geographyCityResolver)
    {
        _context = context;
        _geographyCityResolver = geographyCityResolver;
    }

    public async Task<AdminCustomerFilterOptionsDto> Handle(
        GetAdminCustomerFilterOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var rawCities = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.City != null && address.City != string.Empty)
            .Select(address => address.City!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var cityOptions = new Dictionary<string, AdminCustomerFilterOptionDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawCity in rawCities)
        {
            var labels = CustomerCityLocalization.Localize(_geographyCityResolver, rawCity);
            var value = labels.Code ?? labels.Raw ?? rawCity;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            cityOptions[value] = new AdminCustomerFilterOptionDto(
                value,
                labels.Ar ?? value,
                labels.En ?? value);
        }

        var cities = cityOptions.Values
            .OrderBy(option => option.LabelEn, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AdminCustomerFilterOptionsDto(
            AdminCustomerFilterOptionsFactory.BuildStatuses(),
            cities);
    }
}
