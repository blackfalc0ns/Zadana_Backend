using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;

public record SubmitProductRequestCommand(
    string SuggestedNameAr,
    string SuggestedNameEn,
    Guid SuggestedCategoryId,
    string? SuggestedDescriptionAr = null,
    string? SuggestedDescriptionEn = null,
    string? ImageUrl = null
) : IRequest<Guid>;
