using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;

public record ReviewProductRequestCommand(
    Guid ProductRequestId,
    bool IsApproved,
    string? RejectionReason = null
) : IRequest<Guid?>;
