using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.DeleteBrand;

public record DeleteBrandCommand(Guid Id) : IRequest;
