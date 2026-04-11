using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class Category : BaseEntity
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string? ImageUrl { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation
    public Category? ParentCategory { get; private set; }
    public ICollection<Category> SubCategories { get; private set; } = [];
    public ICollection<ProductType> ProductTypes { get; private set; } = [];
    public ICollection<MasterProduct> MasterProducts { get; private set; } = [];
    public ICollection<Brand> Brands { get; private set; } = [];

    private Category() { }

    public Category(string nameAr, string nameEn, string? imageUrl = null, Guid? parentCategoryId = null, int displayOrder = 0)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        ImageUrl = imageUrl;
        ParentCategoryId = parentCategoryId;
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public void Update(string nameAr, string nameEn, string? imageUrl, Guid? parentCategoryId, int displayOrder)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        ImageUrl = imageUrl;
        
        // Prevent self-referencing parent
        if (parentCategoryId == Id)
            throw new InvalidOperationException("Category cannot be its own parent.");
            
        ParentCategoryId = parentCategoryId;
        DisplayOrder = displayOrder;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
