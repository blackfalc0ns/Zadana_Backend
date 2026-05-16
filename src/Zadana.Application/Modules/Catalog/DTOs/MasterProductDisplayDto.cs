using System.Globalization;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record MasterProductVariantOptionDto(
    Guid Id,
    Guid? DefaultVendorProductId,
    string NameAr,
    string NameEn,
    string? DisplaySizeAr,
    string? DisplaySizeEn,
    bool IsCurrent);

public static class MasterProductDisplayDto
{
    public static MasterProductDto ToDto(
        MasterProduct product,
        bool isInVendorStore,
        ICollection<MasterProductVariantOptionDto>? variants = null)
    {
        var measurementUnit = product.MeasurementUnit ?? product.UnitOfMeasure;

        return new MasterProductDto(
            product.Id,
            product.NameAr,
            product.NameEn,
            product.Slug,
            product.DescriptionAr,
            product.DescriptionEn,
            product.Barcode,
            product.CategoryId,
            product.BrandId,
            product.Brand?.NameAr,
            product.Brand?.NameEn,
            product.MeasurementUnitId,
            measurementUnit?.NameAr,
            measurementUnit?.NameEn,
            product.PackageTypeId,
            product.PackageType?.NameAr,
            product.PackageType?.NameEn,
            product.MeasurementValue,
            product.MeasurementUnitId,
            measurementUnit?.NameAr,
            measurementUnit?.NameEn,
            product.VariantGroupId,
            BuildDisplaySize(product.PackageType?.NameAr, product.MeasurementValue, measurementUnit?.NameAr, measurementUnit?.Symbol, true),
            BuildDisplaySize(product.PackageType?.NameEn, product.MeasurementValue, measurementUnit?.NameEn, measurementUnit?.Symbol, false),
            product.Status.ToString(),
            isInVendorStore,
            product.Images.Select(i => new MasterProductImageDto(i.Url, i.AltText, i.DisplayOrder, i.IsPrimary)).ToList(),
            product.CreatedAtUtc,
            product.UpdatedAtUtc,
            variants);
    }

    public static string? BuildDisplaySize(
        string? packageTypeName,
        decimal? measurementValue,
        string? measurementUnitName,
        string? measurementUnitSymbol,
        bool isArabic)
    {
        var packaging = Normalize(packageTypeName);
        var measurementUnit = Normalize(!string.IsNullOrWhiteSpace(measurementUnitSymbol) ? measurementUnitSymbol : measurementUnitName);

        if (measurementValue.HasValue && !string.IsNullOrWhiteSpace(measurementUnit))
        {
            var value = FormatMeasurementValue(measurementValue.Value);
            return string.IsNullOrWhiteSpace(packaging)
                ? $"{value} {measurementUnit}"
                : $"{packaging} {value} {measurementUnit}";
        }

        if (!string.IsNullOrWhiteSpace(packaging))
        {
            return packaging;
        }

        return measurementUnit;
    }

    public static string? BuildLegacyUnit(
        string? packageTypeName,
        string? measurementUnitName,
        bool isArabic)
    {
        _ = isArabic;

        var measurementUnit = Normalize(measurementUnitName);
        if (!string.IsNullOrWhiteSpace(measurementUnit))
        {
            return measurementUnit;
        }

        return Normalize(packageTypeName);
    }

    private static string FormatMeasurementValue(decimal value)
    {
        return value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
