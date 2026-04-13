using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class HomeSection : BaseEntity
{
    public Guid CategoryId { get; private set; }
    public HomeSectionTheme Theme { get; private set; }
    public int DisplayOrder { get; private set; }
    public int ProductsTake { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }

    public Category Category { get; private set; } = null!;

    private HomeSection() { }

    public HomeSection(
        Guid categoryId,
        HomeSectionTheme theme,
        int displayOrder,
        int productsTake = 10,
        DateTime? startsAtUtc = null,
        DateTime? endsAtUtc = null)
    {
        ApplyState(categoryId, theme, displayOrder, productsTake, startsAtUtc, endsAtUtc);
        IsActive = true;
    }

    public void Update(
        Guid categoryId,
        HomeSectionTheme theme,
        int displayOrder,
        int productsTake,
        DateTime? startsAtUtc,
        DateTime? endsAtUtc,
        bool isActive)
    {
        ApplyState(categoryId, theme, displayOrder, productsTake, startsAtUtc, endsAtUtc);
        IsActive = isActive;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    private void ApplyState(
        Guid categoryId,
        HomeSectionTheme theme,
        int displayOrder,
        int productsTake,
        DateTime? startsAtUtc,
        DateTime? endsAtUtc)
    {
        if (categoryId == Guid.Empty)
        {
            throw new BusinessRuleException("INVALID_HOME_SECTION_CATEGORY", "Category is required.");
        }

        if (displayOrder < 0)
        {
            throw new BusinessRuleException("INVALID_DISPLAY_ORDER", "Display order cannot be negative.");
        }

        if (productsTake <= 0 || productsTake > 20)
        {
            throw new BusinessRuleException("INVALID_HOME_SECTION_PRODUCTS_TAKE", "Products take must be between 1 and 20.");
        }

        if (startsAtUtc.HasValue && endsAtUtc.HasValue && endsAtUtc < startsAtUtc)
        {
            throw new BusinessRuleException("INVALID_DATE_RANGE", "EndsAtUtc must be greater than or equal to StartsAtUtc.");
        }

        CategoryId = categoryId;
        Theme = theme;
        DisplayOrder = displayOrder;
        ProductsTake = productsTake;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
    }
}
