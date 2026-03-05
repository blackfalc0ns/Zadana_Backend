using MediatR;

namespace Zadana.Application.Modules.Vendors.Commands.SuspendVendor;

public record SuspendVendorCommand(
    Guid VendorId,
    string Reason) : IRequest;
