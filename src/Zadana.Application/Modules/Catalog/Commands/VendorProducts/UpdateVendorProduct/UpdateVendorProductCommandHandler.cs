using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.UpdateVendorProduct;

public class UpdateVendorProductCommandHandler : IRequestHandler<UpdateVendorProductCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateVendorProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateVendorProductCommand request, CancellationToken cancellationToken)
    {
        var vendorProduct = await _context.VendorProducts
            .FirstOrDefaultAsync(vp => vp.Id == request.Id && vp.VendorId == request.VendorId, cancellationToken);

        if (vendorProduct == null)
            throw new NotFoundException(nameof(VendorProduct), request.Id);

        vendorProduct.UpdatePricing(request.SellingPrice, request.CompareAtPrice);
        vendorProduct.UpdateStock(request.StockQty);
        vendorProduct.UpdateCustomDetails(
            request.CustomNameAr, 
            request.CustomNameEn, 
            request.CustomDescriptionAr, 
            request.CustomDescriptionEn);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
