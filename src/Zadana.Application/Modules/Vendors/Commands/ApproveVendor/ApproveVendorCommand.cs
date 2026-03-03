using MediatR;

namespace Zadana.Application.Modules.Vendors.Commands.ApproveVendor;

public record ApproveVendorCommand(
    Guid VendorId,
    decimal CommissionRate) : IRequest;
