using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.ChangeStatus;

public record ChangeVendorProductStatusCommand(
    Guid Id,
    Guid VendorId,
    bool IsActive) : IRequest;
