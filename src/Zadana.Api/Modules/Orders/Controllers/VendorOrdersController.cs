using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Common.Export;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Export;
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
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var result = await Sender.Send(
            new GetVendorWorkspaceOrdersQuery(scope.VendorId, scope.BranchId, search, status, paymentMethod, page, pageSize),
            cancellationToken);

        return Ok(new VendorOrdersListResponse(
            result.Items.Select(MapListItem).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportOrders(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? paymentMethod,
        CancellationToken cancellationToken = default)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var result = await Sender.Send(
            new GetVendorWorkspaceOrdersQuery(
                scope.VendorId,
                scope.BranchId,
                search,
                status,
                paymentMethod,
                1,
                ExportLimits.MaxRows),
            cancellationToken);

        var file = ExcelExportBuilder.BuildFromObjects(
            ExportFileResult.StampFileName("vendor-orders", ".xlsx"),
            "Orders",
            [
                new ExportColumn("Order Number", "orderNumber"),
                new ExportColumn("Customer", "customer"),
                new ExportColumn("Phone", "phone"),
                new ExportColumn("Status", "status"),
                new ExportColumn("Payment Status", "paymentStatus"),
                new ExportColumn("Payment Method", "paymentMethod"),
                new ExportColumn("Total", "total"),
                new ExportColumn("Items", "items"),
                new ExportColumn("Placed At", "placedAt"),
                new ExportColumn("Late", "isLate")
            ],
            result.Items,
            order => new Dictionary<string, string?>
            {
                ["orderNumber"] = order.OrderNumber,
                ["customer"] = order.CustomerName,
                ["phone"] = order.CustomerPhone,
                ["status"] = order.Status,
                ["paymentStatus"] = order.PaymentStatus,
                ["paymentMethod"] = order.PaymentMethod,
                ["total"] = order.TotalAmount.ToString("0.##"),
                ["items"] = order.ItemsCount.ToString(),
                ["placedAt"] = order.PlacedAtUtc.ToString("o"),
                ["isLate"] = order.IsLate ? "yes" : "no"
            });

        return ExportFileResult.From(file);
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(VendorOrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VendorOrderDetailResponse>> GetOrderById(Guid orderId, CancellationToken cancellationToken = default)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var result = await Sender.Send(new GetVendorOrderDetailQuery(scope.VendorId, scope.BranchId, orderId), cancellationToken);

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
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var result = await Sender.Send(
            new VendorUpdateOrderStatusCommand(orderId, scope.VendorId, scope.BranchId, OrderStatus.Accepted, "Vendor accepted the order"),
            cancellationToken);
        return Ok(MapResponse(result));
    }

    [HttpPost("{orderId:guid}/reject")]
    public async Task<ActionResult<VendorOrderStatusResponse>> RejectOrder(
        Guid orderId,
        [FromBody] VendorOrderNoteRequest? request,
        CancellationToken cancellationToken = default)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var result = await Sender.Send(
            new VendorUpdateOrderStatusCommand(orderId, scope.VendorId, scope.BranchId, OrderStatus.VendorRejected, request?.Note ?? "Vendor rejected the order"),
            cancellationToken);
        return Ok(MapResponse(result));
    }

    [HttpPost("{orderId:guid}/preparing")]
    public async Task<ActionResult<VendorOrderStatusResponse>> MarkPreparing(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var result = await Sender.Send(
            new VendorUpdateOrderStatusCommand(orderId, scope.VendorId, scope.BranchId, OrderStatus.Preparing, "Vendor started preparing"),
            cancellationToken);
        return Ok(MapResponse(result));
    }

    [HttpPost("{orderId:guid}/ready")]
    public async Task<ActionResult<VendorOrderStatusResponse>> MarkReady(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var result = await Sender.Send(
            new VendorUpdateOrderStatusCommand(orderId, scope.VendorId, scope.BranchId, OrderStatus.ReadyForPickup, "Order is ready for pickup"),
            cancellationToken);
        return Ok(MapResponse(result));
    }

    [HttpPost("{orderId:guid}/confirm-pickup")]
    public async Task<ActionResult<VendorPickupOtpConfirmationResponse>> ConfirmPickupOtp(
        Guid orderId,
        [FromBody] VendorPickupOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var result = await Sender.Send(
            new ConfirmVendorPickupOtpCommand(orderId, scope.VendorId, scope.BranchId, request.OtpCode),
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
                    dto.AssignedDriver.PlateNumber,
                    dto.AssignedDriver.ImageUrl),
            dto.DriverArrivalState,
            dto.DriverArrivalUpdatedAtUtc,
            dto.PickupOtp,
            dto.CanConfirmPickup,
            dto.PickupOtpStatus,
            dto.VendorLocation,
            dto.CustomerLocation,
            dto.DriverLiveLocation,
            dto.Items.Select(item => new VendorOrderItemResponse(
                item.Id,
                item.ProductName,
                item.ProductNameAr,
                item.ProductNameEn,
                item.Quantity,
                item.UnitPrice,
                item.LineTotal,
                item.ImageUrl)).ToList(),
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
    GeoPointDto? VendorLocation,
    GeoPointDto? CustomerLocation,
    DriverLiveLocationDto? DriverLiveLocation,
    List<VendorOrderItemResponse> Items,
    List<VendorOrderTimelineResponse> Timeline);
public record AssignedDriverSummaryResponse(
    Guid Id,
    string Name,
    string? PhoneNumber,
    string VehicleType,
    string PlateNumber,
    string? ImageUrl = null);
public record VendorOrderItemResponse(
    Guid Id,
    string ProductName,
    string ProductNameAr,
    string ProductNameEn,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? ImageUrl = null);
public record VendorOrderTimelineResponse(
    string Status,
    string Label,
    DateTime TimestampUtc,
    bool IsCompleted,
    string? Note);
public record VendorPickupOtpRequest(string OtpCode);
public record VendorPickupOtpConfirmationResponse(Guid OrderId, Guid AssignmentId, string Status, string Message);
