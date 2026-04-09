namespace Zadana.Api.Modules.Orders.Requests;

public record AddCartItemRequest(
    Guid ProductId,
    int Quantity);

public record UpdateCartItemQuantityRequest(
    int Quantity);
