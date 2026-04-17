using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.CategoryRequests.SubmitRequest;

public record SubmitCategoryRequestCommand(
    string NameAr,
    string NameEn,
    string TargetLevel,
    Guid? ParentCategoryId = null,
    int DisplayOrder = 1,
    string? ImageUrl = null) : IRequest<Guid>;
