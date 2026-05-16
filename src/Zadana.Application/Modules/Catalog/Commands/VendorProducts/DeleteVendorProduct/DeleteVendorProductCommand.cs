using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.DeleteVendorProduct;

public record DeleteVendorProductCommand(Guid Id, Guid VendorId) : IRequest;
