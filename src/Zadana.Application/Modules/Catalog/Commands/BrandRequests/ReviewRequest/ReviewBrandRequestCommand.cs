using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.BrandRequests.ReviewRequest;

public record ReviewBrandRequestCommand(
    Guid BrandRequestId,
    bool IsApproved,
    string? RejectionReason = null) : IRequest<Guid?>;
