using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class Brand : BaseEntity, ISoftDeletable
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? LogoUrl { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public Guid? CategoryId { get; private set; }
    public bool IsActive { get; private set; }

    // Soft delete
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public void SoftDelete() { IsDeleted = true; DeletedAtUtc = DateTime.UtcNow; }
    public void Restore()    { IsDeleted = false; DeletedAtUtc = null; }

    // Navigation
    public Category? Category { get; private set; }
    public ICollection<BrandCategory> BrandCategories { get; private set; } = [];
    public ICollection<MasterProduct> MasterProducts { get; private set; } = [];

    private Brand() { }

    public Brand(string nameAr, string nameEn, string? logoUrl = null, string? coverImageUrl = null, Guid? categoryId = null)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new InvalidOperationException("Arabic brand name is required.");
        if (string.IsNullOrWhiteSpace(nameEn))
            throw new InvalidOperationException("English brand name is required.");

        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        LogoUrl = logoUrl?.Trim();
        CoverImageUrl = coverImageUrl?.Trim();
        CategoryId = categoryId;
        IsActive = true;
    }

    public void Update(string nameAr, string nameEn, string? logoUrl, string? coverImageUrl, Guid? categoryId)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        LogoUrl = logoUrl?.Trim();
        CoverImageUrl = coverImageUrl?.Trim();
        CategoryId = categoryId;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
