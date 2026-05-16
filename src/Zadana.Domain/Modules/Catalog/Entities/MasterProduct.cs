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
    public Guid? PackageTypeId { get; private set; }
    public decimal? MeasurementValue { get; private set; }
    public Guid? MeasurementUnitId { get; private set; }
    public Guid VariantGroupId { get; private set; }
    public Guid? ProductTypeId { get; private set; }
    public Guid? PartId { get; private set; }
    public ProductStatus Status { get; private set; }

    // Navigation
    public Category Category { get; private set; } = null!;
    public Brand? Brand { get; private set; }
    public UnitOfMeasure? UnitOfMeasure { get; private set; }
    public UnitOfMeasure? PackageType { get; private set; }
    public UnitOfMeasure? MeasurementUnit { get; private set; }
    public ProductType? ProductType { get; private set; }
    public Part? Part { get; private set; }
    public ICollection<MasterProductImage> Images { get; private set; } = [];

    private MasterProduct() { }

    public MasterProduct(
        string nameAr,
        string nameEn,
        string slug,

        
        Guid categoryId,
        Guid? brandId = null,
        Guid? unitOfMeasureId = null,
        Guid? packageTypeId = null,
        decimal? measurementValue = null,
        Guid? measurementUnitId = null,
        string? descriptionAr = null,
        string? descriptionEn = null,
        string? barcode = null,
        Guid? productTypeId = null,
        Guid? partId = null,
        Guid? variantGroupId = null)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        Slug = slug.Trim();
        CategoryId = categoryId;
        BrandId = brandId;
        UnitOfMeasureId = measurementUnitId ?? unitOfMeasureId;
        PackageTypeId = packageTypeId;
        MeasurementValue = measurementValue;
        MeasurementUnitId = measurementUnitId ?? unitOfMeasureId;
        VariantGroupId = variantGroupId ?? Guid.Empty;
        ProductTypeId = productTypeId;
        PartId = partId;
        DescriptionAr = descriptionAr?.Trim();
        DescriptionEn = descriptionEn?.Trim();
        Barcode = barcode?.Trim();
        Status = ProductStatus.Draft;
    }

    public MasterProduct(
        string nameAr,
        string nameEn,
        string slug,
        Guid categoryId,
        Guid? legacyBrandId,
        Guid? legacyUnitOfMeasureId,
        string? legacyDescriptionAr,
        string? legacyDescriptionEn,
        string? legacyBarcode = null,
        Guid? legacyProductTypeId = null,
        Guid? legacyPartId = null)
        : this(
            nameAr,
            nameEn,
            slug,
            categoryId,
            legacyBrandId,
            legacyUnitOfMeasureId,
            null,
            null,
            null,
            legacyDescriptionAr,
            legacyDescriptionEn,
            legacyBarcode,
            legacyProductTypeId,
            legacyPartId,
            null)
    {
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
    public void ChangeUnit(Guid? unitOfMeasureId)
    {
        UnitOfMeasureId = unitOfMeasureId;
        MeasurementUnitId = unitOfMeasureId;
    }
    public void ChangePackageType(Guid? packageTypeId) => PackageTypeId = packageTypeId;
    public void ChangeMeasurement(decimal? measurementValue, Guid? measurementUnitId)
    {
        MeasurementValue = measurementValue;
        MeasurementUnitId = measurementUnitId;
        UnitOfMeasureId = measurementUnitId;
    }
    public void ChangeVariantGroup(Guid variantGroupId) => VariantGroupId = variantGroupId;
    public void ChangeProductType(Guid? productTypeId) => ProductTypeId = productTypeId;
    public void ChangePart(Guid? partId) => PartId = partId;
    public void SetStatus(ProductStatus status) => Status = status;

    public void AddImage(string url, string? altText = null, int displayOrder = 0, bool isPrimary = false)
    {
        Images.Add(new MasterProductImage(Id, url, altText, displayOrder, isPrimary));
    }

    public void ClearImages() => Images.Clear();

    public void Publish() => Status = ProductStatus.Active;
    public void Unpublish() => Status = ProductStatus.Inactive;
    public void Discontinue() => Status = ProductStatus.Discontinued;
}
