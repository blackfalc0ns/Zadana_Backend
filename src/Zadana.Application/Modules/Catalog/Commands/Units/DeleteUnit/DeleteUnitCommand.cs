using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.Units.DeleteUnit;

public record DeleteUnitCommand(Guid Id) : IRequest;
