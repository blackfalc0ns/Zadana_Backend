using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AddVendorBranch;

public class AddVendorBranchCommandHandler : IRequestHandler<AddVendorBranchCommand, Guid>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddVendorBranchCommandHandler(IVendorRepository vendorRepository, IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(AddVendorBranchCommand request, CancellationToken cancellationToken)
    {
        var vendorExists = await _vendorRepository.ExistsAsync(request.VendorId, cancellationToken);
        if (!vendorExists)
        {
            throw new NotFoundException("Vendor", request.VendorId);
        }

        // 2. Create the Vendor Branch
        // Provide a default delivery radius of 5.0 for this simple implementation
        var branch = new VendorBranch(
            vendorId: request.VendorId,
            name: request.Name,
            addressLine: request.AddressLine,
            latitude: request.Latitude ?? 0,
            longitude: request.Longitude ?? 0,
            contactPhone: request.Phone ?? string.Empty,
            deliveryRadiusKm: 5.0m 
        );

        // Optional: Adding Operating Hours based on OpensAt / ClosesAt could be wired here
        // if Domain supported it via parameters or helper methods.

        // 3. Save to database
        _vendorRepository.AddBranch(branch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return branch.Id;
    }
}
