using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.SetMasterProductCardPriceVisibility;

public record SetMasterProductCardPriceVisibilityCommand(
    Guid ProductId,
    bool ShowPriceOnCard) : IRequest<Unit>;
