using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.CategoryRequests.ReviewRequest;

public record ReviewCategoryRequestCommand(
    Guid CategoryRequestId,
    bool IsApproved,
    string? RejectionReason = null) : IRequest<Guid?>;
