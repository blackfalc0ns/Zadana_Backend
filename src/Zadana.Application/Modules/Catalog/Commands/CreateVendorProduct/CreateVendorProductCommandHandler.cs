using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.CreateVendorProduct;

public class CreateVendorProductCommandHandler : IRequestHandler<CreateVendorProductCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateVendorProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateVendorProductCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        if (vendor.Status != VendorStatus.Active)
        {
            throw new BusinessRuleException("VENDOR_NOT_VERIFIED", "لا يمكنك إضافة منتجات حتى يتم توثيق حسابك من الإدارة. | You cannot add products until your account is verified by the administration.");
        }

        var masterProductExists = _context.MasterProducts.Any(mp => mp.Id == request.MasterProductId);
        if (!masterProductExists)
        {
            throw new NotFoundException("MasterProduct", request.MasterProductId);
        }

        var vendorProduct = new VendorProduct(
            vendorId: request.VendorId,
            masterProductId: request.MasterProductId,
            sellingPrice: request.SellingPrice,
            stockQuantity: request.StockQty,
            compareAtPrice: request.CompareAtPrice,
            vendorBranchId: request.BranchId
        );

        _context.VendorProducts.Add(vendorProduct);
        await _context.SaveChangesAsync(cancellationToken);

        return vendorProduct.Id;
    }
}
