using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Enums;

namespace Zadana.Application.Modules.Marketing;

internal static class MarketingDatabaseObjectFallbacks
{
    public static bool IsMissingDatabaseObject(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static List<HomeContentSectionSettingDto> CreateDefaultSectionSettings() =>
        Enum.GetValues<HomeContentSectionType>()
            .Select(sectionType => new HomeContentSectionSettingDto(sectionType.ToString(), true))
            .ToList();
}
