using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.BrandRequests.SubmitRequest;

public record SubmitBrandRequestCommand(
    string NameAr,
    string NameEn,
    string? LogoUrl = null) : IRequest<Guid>;
