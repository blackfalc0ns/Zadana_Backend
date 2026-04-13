namespace Zadana.Application.Modules.Identity.Commands.DeleteCustomerAddress;

public record DeleteCustomerAddressCommand(Guid AddressId, Guid UserId) : MediatR.IRequest;
