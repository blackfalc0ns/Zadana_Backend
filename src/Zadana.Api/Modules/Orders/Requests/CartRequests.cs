namespace Zadana.Api.Modules.Orders.Requests;

public record AddCartItemRequest(
    Guid ProductId,
    int Quantity,
    Guid? VendorId);

public record UpdateCartItemQuantityRequest(
    int Quantity);
