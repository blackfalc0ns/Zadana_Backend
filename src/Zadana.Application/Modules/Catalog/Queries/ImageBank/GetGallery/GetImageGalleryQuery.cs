using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Queries.ImageBank.GetGallery;

public record GetImageGalleryQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null
) : IRequest<PaginatedList<ImageBankDto>>;

public record ImageBankDto(
    Guid Id,
    string Url,
    string? AltText,
    string? Tags,
    string Status,
    string? RejectionReason
);
