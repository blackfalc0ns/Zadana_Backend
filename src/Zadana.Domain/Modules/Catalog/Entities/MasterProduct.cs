using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class MasterProduct : BaseEntity
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public string? Barcode { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? BrandId { get; private set; }
    public Guid? UnitOfMeasureId { get; private set; }
    public ProductStatus Status { get; private set; }

    // Navigation
    public Category Category { get; private set; } = null!;
    public Brand? Brand { get; private set; }
    public UnitOfMeasure? UnitOfMeasure { get; private set; }
    public ICollection<MasterProductImage> Images { get; private set; } = [];

    private MasterProduct() { }

    public MasterProduct(
        string nameAr,
        string nameEn,
        string slug,
        Guid categoryId,
        Guid? brandId = null,
        Guid? unitOfMeasureId = null,
        string? descriptionAr = null,
        string? descriptionEn = null,
        string? barcode = null)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        Slug = slug.Trim();
        CategoryId = categoryId;
        BrandId = brandId;
        UnitOfMeasureId = unitOfMeasureId;
        DescriptionAr = descriptionAr?.Trim();
        DescriptionEn = descriptionEn?.Trim();
        Barcode = barcode?.Trim();
        Status = ProductStatus.Draft;
    }

    public void UpdateDetails(
        string nameAr,
        string nameEn,
        string slug,
        string? descriptionAr,
        string? descriptionEn,
        string? barcode)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        Slug = slug.Trim();
        DescriptionAr = descriptionAr?.Trim();
        DescriptionEn = descriptionEn?.Trim();
        Barcode = barcode?.Trim();
    }

    public void ChangeCategory(Guid categoryId) => CategoryId = categoryId;
    public void ChangeBrand(Guid? brandId) => BrandId = brandId;
    public void ChangeUnit(Guid? unitOfMeasureId) => UnitOfMeasureId = unitOfMeasureId;

    public void AddImage(string url, string? altText = null, int displayOrder = 0, bool isPrimary = false)
    {
        Images.Add(new MasterProductImage(Id, url, altText, displayOrder, isPrimary));
    }

    public void ClearImages() => Images.Clear();

    public void Publish() => Status = ProductStatus.Active;
    public void Unpublish() => Status = ProductStatus.Inactive;
    public void Discontinue() => Status = ProductStatus.Discontinued;
}
