using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Commands.ConfirmVendorPickupOtp;
using Zadana.Application.Modules.Orders.Commands.VendorUpdateOrderStatus;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Queries.GetVendorOrderDetail;
using Zadana.Application.Modules.Orders.Queries.GetVendorWorkspaceOrders;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/vendor/orders")]
[Tags("Vendor App API")]
[Authorize(Policy = "VendorOnly")]
public class VendorOrdersController : ApiControllerBase
{
    private readonly ICurrentVendorService _currentVendorService;

    public VendorOrdersController(ICurrentVendorService currentVendorService)
    {
        _currentVendorService = currentVendorService;
    }

    [HttpGet]
    public async Task<ActionResult<VendorOrdersListResponse>> GetOrders(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? paymentMethod,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var result = await Sender.Send(
            new GetVendorWorkspaceOrdersQuery(vendorId, search, status, paymentMethod, page, pageSize),
            cancellationToken);

        return Ok(new VendorOrdersListResponse(
            result.Items.Select(MapListItem).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<VendorOrderDetailResponse>> GetOrderById(Guid orderId, CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var result = await Sender.Send(new GetVendorOrderDetailQuery(vendorId, orderId), cancellationToken);

        if (result is null)
        {
            throw new NotFoundException("Order", orderId);
        }

        return Ok(MapDetail(result));
    }

    [HttpPost("{orderId:guid}/accept")]
    public async Task<ActionResult<VendorOrderStatusResponse>> AcceptOrder(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var result = await Sender.Send(
            new VendorUpdateOrderStatusCommand(orderId, vendorId, OrderStatus.Accepted, "Vendor accepted the order"),
            cancellationToken);
        return Ok(MapResponse(result));
    }

    [HttpPost("{orderId:guid}/reject")]
    public async Task<ActionResult<VendorOrderStatusResponse>> RejectOrder(
        Guid orderId,
        [FromBody] VendorOrderNoteRequest? request,
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var result = await Sender.Send(
            new VendorUpdateOrderStatusCommand(orderId, vendorId, OrderStatus.VendorRejected, request?.Note ?? "Vendor rejected the order"),
            cancellationToken);
        return Ok(MapResponse(result));
    }

    [HttpPost("{orderId:guid}/preparing")]
    public async Task<ActionResult<VendorOrderStatusResponse>> MarkPreparing(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var result = await Sender.Send(
            new VendorUpdateOrderStatusCommand(orderId, vendorId, OrderStatus.Preparing, "Vendor started preparing"),
            cancellationToken);
        return Ok(MapResponse(result));
    }

    [HttpPost("{orderId:guid}/ready")]
    public async Task<ActionResult<VendorOrderStatusResponse>> MarkReady(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var result = await Sender.Send(
            new VendorUpdateOrderStatusCommand(orderId, vendorId, OrderStatus.ReadyForPickup, "Order is ready for pickup"),
            cancellationToken);
        return Ok(MapResponse(result));
    }

    [HttpPost("{orderId:guid}/confirm-pickup")]
    public async Task<ActionResult<VendorPickupOtpConfirmationResponse>> ConfirmPickupOtp(
        Guid orderId,
        [FromBody] VendorPickupOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var result = await Sender.Send(
            new ConfirmVendorPickupOtpCommand(orderId, vendorId, request.OtpCode),
            cancellationToken);

        return Ok(new VendorPickupOtpConfirmationResponse(result.OrderId, result.AssignmentId, result.Status, result.Message));
    }

    private static VendorOrderStatusResponse MapResponse(VendorUpdateOrderStatusResultDto dto) =>
        new(dto.OrderId, dto.Status, dto.Message);

    private static VendorOrderListItemResponse MapListItem(VendorOrderListItemDto dto) =>
        new(
            dto.Id,
            dto.OrderNumber,
            dto.CustomerName,
            dto.CustomerPhone,
            dto.Status,
            dto.PaymentStatus,
            dto.PaymentMethod,
            dto.TotalAmount,
            dto.ItemsCount,
            dto.PlacedAtUtc,
            dto.IsLate);

    private static VendorOrderDetailResponse MapDetail(VendorOrderDetailDto dto) =>
        new(
            dto.Id,
            dto.OrderNumber,
            dto.CustomerName,
            dto.CustomerPhone,
            dto.CustomerAddress,
            dto.Status,
            dto.PaymentStatus,
            dto.PaymentMethod,
            dto.Subtotal,
            dto.DeliveryFee,
            dto.TotalAmount,
            dto.Notes,
            dto.PlacedAtUtc,
            dto.AssignedDriver is null
                ? null
                : new AssignedDriverSummaryResponse(
                    dto.AssignedDriver.Id,
                    dto.AssignedDriver.Name,
                    dto.AssignedDriver.PhoneNumber,
                    dto.AssignedDriver.VehicleType,
                    dto.AssignedDriver.PlateNumber),
            dto.DriverArrivalState,
            dto.DriverArrivalUpdatedAtUtc,
            dto.PickupOtp,
            dto.CanConfirmPickup,
            dto.PickupOtpStatus,
            dto.Items.Select(item => new VendorOrderItemResponse(
                item.Id,
                item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.LineTotal)).ToList(),
            dto.Timeline.Select(item => new VendorOrderTimelineResponse(
                item.Status,
                item.Label,
                item.TimestampUtc,
                item.IsCompleted,
                item.Note)).ToList());
}

public record VendorOrderNoteRequest(string? Note);

public record VendorOrderStatusResponse(Guid OrderId, string Status, string Message);
public record VendorOrdersListResponse(
    List<VendorOrderListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
public record VendorOrderListItemResponse(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhone,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    decimal TotalAmount,
    int ItemsCount,
    DateTime PlacedAtUtc,
    bool IsLate);
public record VendorOrderDetailResponse(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhone,
    string CustomerAddress,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal TotalAmount,
    string? Notes,
    DateTime PlacedAtUtc,
    AssignedDriverSummaryResponse? AssignedDriver,
    string DriverArrivalState,
    DateTime? DriverArrivalUpdatedAtUtc,
    string? PickupOtp,
    bool CanConfirmPickup,
    string PickupOtpStatus,
    List<VendorOrderItemResponse> Items,
    List<VendorOrderTimelineResponse> Timeline);
public record AssignedDriverSummaryResponse(
    Guid Id,
    string Name,
    string? PhoneNumber,
    string VehicleType,
    string PlateNumber);
public record VendorOrderItemResponse(
    Guid Id,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
public record VendorOrderTimelineResponse(
    string Status,
    string Label,
    DateTime TimestampUtc,
    bool IsCompleted,
    string? Note);
public record VendorPickupOtpRequest(string OtpCode);
public record VendorPickupOtpConfirmationResponse(Guid OrderId, Guid AssignmentId, string Status, string Message);
