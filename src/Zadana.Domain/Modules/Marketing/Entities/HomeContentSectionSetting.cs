using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class HomeContentSectionSetting : BaseEntity
{
    public HomeContentSectionType SectionType { get; private set; }
    public bool IsEnabled { get; private set; }

    private HomeContentSectionSetting() { }

    public HomeContentSectionSetting(HomeContentSectionType sectionType, bool isEnabled = true)
    {
        SectionType = sectionType;
        IsEnabled = isEnabled;
    }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
    public void Activate() => IsEnabled = true;
    public void Deactivate() => IsEnabled = false;
}
