namespace Zadana.Application.Modules.Identity.Commands.SetDefaultCustomerAddress;

public record SetDefaultCustomerAddressCommand(Guid AddressId, Guid UserId) : MediatR.IRequest;
