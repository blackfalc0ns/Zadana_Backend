using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.BrandRequests.SubmitRequest;

public record SubmitBrandRequestCommand(
    Guid CategoryId,
    string NameAr,
    string NameEn,
    string? LogoUrl = null) : IRequest<Guid>;
