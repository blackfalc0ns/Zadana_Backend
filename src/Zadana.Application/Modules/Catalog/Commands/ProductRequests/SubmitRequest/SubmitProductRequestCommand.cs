using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;

public record RequestedBrandDraft(
    Guid CategoryId,
    string NameAr,
    string NameEn,
    string? LogoUrl = null);

public record RequestedCategoryDraft(
    string NameAr,
    string NameEn,
    string TargetLevel,
    Guid? ParentCategoryId = null,
    int DisplayOrder = 1,
    string? ImageUrl = null);

public record SubmitProductRequestCommand(
    string SuggestedNameAr,
    string SuggestedNameEn,
    Guid? SuggestedCategoryId = null,
    Guid? SuggestedBrandId = null,
    Guid? SuggestedUnitOfMeasureId = null,
    Guid? SuggestedPackageTypeId = null,
    decimal? SuggestedMeasurementValue = null,
    string? SuggestedDescriptionAr = null,
    string? SuggestedDescriptionEn = null,
    string? ImageUrl = null,
    IReadOnlyList<string>? SuggestedImageUrls = null,
    RequestedBrandDraft? RequestedBrand = null,
    RequestedCategoryDraft? RequestedCategory = null
) : IRequest<Guid>;
