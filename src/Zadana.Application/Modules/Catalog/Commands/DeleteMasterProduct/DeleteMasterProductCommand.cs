using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.DeleteMasterProduct;

public record DeleteMasterProductCommand(Guid Id) : IRequest;
