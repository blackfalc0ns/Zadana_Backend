using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.Commands.PlaceCheckoutOrder;
using Zadana.Application.Modules.Orders.Commands.CancelCustomerOrder;
using Zadana.Application.Modules.Orders.Commands.CreateOrderComplaint;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Queries.GetCustomerOrderComplaint;
using Zadana.Application.Modules.Orders.Queries.GetCustomerOrderDetail;
using Zadana.Application.Modules.Orders.Queries.GetCustomerOrders;
using Zadana.Application.Modules.Orders.Queries.GetCustomerOrderTracking;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/orders")]
[Tags("Customer App API")]
[Authorize(Policy = "CustomerOnly")]
public class OrdersController : ApiControllerBase
{
    private const string DeviceIdHeader = "X-Device-Id";
    private readonly ICurrentUserService _currentUserService;

    public OrdersController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpGet("active")]
    public async Task<ActionResult<CustomerOrdersResponse>> GetActiveOrders(
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetCustomerOrdersQuery(userId, CustomerOrderBucket.Active, page, perPage), cancellationToken);
        return Ok(MapOrders(result));
    }

    [HttpGet("completed")]
    public async Task<ActionResult<CustomerOrdersResponse>> GetCompletedOrders(
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetCustomerOrdersQuery(userId, CustomerOrderBucket.Completed, page, perPage), cancellationToken);
        return Ok(MapOrders(result));
    }

    [HttpGet("returns")]
    public async Task<ActionResult<CustomerOrdersResponse>> GetReturnOrders(
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetCustomerOrdersQuery(userId, CustomerOrderBucket.Returns, page, perPage), cancellationToken);
        return Ok(MapOrders(result));
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<CustomerOrderDetailResponse>> GetOrderDetail(Guid orderId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetCustomerOrderDetailQuery(orderId, userId), cancellationToken);
        return Ok(MapOrderDetail(result));
    }

    [HttpGet("{orderId:guid}/tracking")]
    public async Task<ActionResult<CustomerOrderTrackingResponse>> GetOrderTracking(Guid orderId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetCustomerOrderTrackingQuery(orderId, userId), cancellationToken);
        return Ok(MapOrderTracking(result));
    }

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<ActionResult<CancelCustomerOrderResponse>> CancelOrder(
        Guid orderId,
        [FromBody] CancelCustomerOrderRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new CancelCustomerOrderCommand(orderId, userId, request.Reason, request.Note), cancellationToken);

        return Ok(new CancelCustomerOrderResponse(
            result.Message,
            new CancelledOrderStatusResponse(result.Id, result.Status)));
    }

    [HttpPost("{orderId:guid}/complaints")]
    public async Task<ActionResult<CreateOrderComplaintResponse>> CreateComplaint(
        Guid orderId,
        [FromBody] CreateOrderComplaintRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new CreateOrderComplaintCommand(
                orderId,
                userId,
                request.Message,
                request.Attachments?.Select(x => new CreateOrderComplaintAttachmentItem(x.FileName, x.FileUrl)).ToList()),
            cancellationToken);

        return Ok(new CreateOrderComplaintResponse(
            "complaint submitted successfully",
            MapComplaint(result)));
    }

    [HttpGet("{orderId:guid}/complaints")]
    public async Task<ActionResult<GetOrderComplaintResponse>> GetComplaint(Guid orderId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetCustomerOrderComplaintQuery(orderId, userId), cancellationToken);
        return Ok(new GetOrderComplaintResponse(MapComplaint(result)));
    }

    [HttpPost]
    public async Task<ActionResult<PlaceOrderResponse>> PlaceOrder(
        [FromBody] PlaceOrderRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new PlaceCheckoutOrderCommand(
                userId,
                request.AddressId,
                request.DeliverySlotId,
                request.PaymentMethod,
                request.PromoCode,
                request.Notes,
                ResolveDeviceIdHeader()),
            cancellationToken);

        return Ok(CheckoutController.MapPlacedOrder(result));
    }

    private static CustomerOrdersResponse MapOrders(CustomerOrderListDto dto) =>
        new(
            dto.Items.Select(MapOrderListItem).ToList(),
            dto.Page,
            dto.PerPage,
            dto.Total);

    private static CustomerOrderListItemResponse MapOrderListItem(CustomerOrderListItemDto dto) =>
        new(
            dto.Id,
            dto.CreatedAt,
            dto.TotalPrice,
            dto.Status,
            dto.ItemsCount,
            dto.Items.Select(MapOrderProduct).ToList());

    private static CustomerOrderProductResponse MapOrderProduct(CustomerOrderProductDto dto) =>
        new(dto.Id, dto.Name, dto.Quantity, dto.Price);

    private static CustomerOrderDetailResponse MapOrderDetail(CustomerOrderDetailDto dto) =>
        new(
            dto.Id,
            dto.CreatedAt,
            dto.TotalPrice,
            dto.Status,
            dto.CanCancel,
            dto.ItemsCount,
            new CustomerOrderSummaryResponse(
                dto.Summary.Subtotal,
                dto.Summary.ShippingCost,
                dto.Summary.Total),
            dto.Items.Select(MapOrderProduct).ToList());

    private static CustomerOrderTrackingResponse MapOrderTracking(CustomerOrderTrackingDto dto) =>
        new(
            new CustomerOrderTrackingOrderResponse(dto.Order.Id, dto.Order.Status),
            dto.EstimatedDelivery is null
                ? null
                : new CustomerOrderEstimatedDeliveryResponse(dto.EstimatedDelivery.Datetime, dto.EstimatedDelivery.Formatted),
            dto.Driver is null
                ? null
                : new CustomerOrderTrackingDriverResponse(dto.Driver.Id, dto.Driver.Name, dto.Driver.PhoneNumber, dto.Driver.Subtitle),
            dto.Timeline
                .Select(item => new CustomerOrderTrackingTimelineItemResponse(
                    item.Id,
                    item.Title,
                    item.Time,
                    item.IsActive,
                    item.IsCompleted))
                .ToList());

    private static OrderComplaintResponse MapComplaint(OrderComplaintDto dto) =>
        new(
            dto.Id,
            dto.Status,
            dto.Message,
            dto.Attachments.Select(x => new OrderComplaintAttachmentResponse(x.FileName, x.FileUrl)).ToList(),
            dto.CreatedAt);

    private string? ResolveDeviceIdHeader()
    {
        var deviceId = Request.Headers[DeviceIdHeader].ToString();
        return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
    }
}
