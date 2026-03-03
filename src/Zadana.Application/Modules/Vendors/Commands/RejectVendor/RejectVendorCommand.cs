using MediatR;

namespace Zadana.Application.Modules.Vendors.Commands.RejectVendor;

public record RejectVendorCommand(
    Guid VendorId,
    string Reason) : IRequest;
