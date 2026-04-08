using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Enums;

namespace Zadana.Application.Modules.Marketing.Queries.HomeContentSections;

public record GetHomeContentSectionSettingsQuery() : IRequest<List<HomeContentSectionSettingDto>>;

public class GetHomeContentSectionSettingsQueryHandler : IRequestHandler<GetHomeContentSectionSettingsQuery, List<HomeContentSectionSettingDto>>
{
    private readonly IApplicationDbContext _context;
    public GetHomeContentSectionSettingsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<HomeContentSectionSettingDto>> Handle(GetHomeContentSectionSettingsQuery request, CancellationToken cancellationToken)
    {
        Dictionary<HomeContentSectionType, bool> existing;
        try
        {
            existing = await _context.HomeContentSectionSettings
                .AsNoTracking()
                .ToDictionaryAsync(x => x.SectionType, x => x.IsEnabled, cancellationToken);
        }
        catch (Exception ex) when (MarketingDatabaseObjectFallbacks.IsMissingDatabaseObject(ex))
        {
            return MarketingDatabaseObjectFallbacks.CreateDefaultSectionSettings();
        }

        return Enum.GetValues<HomeContentSectionType>()
            .Select(sectionType => new HomeContentSectionSettingDto(
                sectionType.ToString(),
                existing.TryGetValue(sectionType, out var isEnabled) ? isEnabled : true))
            .ToList();
    }
}
