using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.Commands.PlaceCheckoutOrder;
using Zadana.Application.Modules.Files.Commands.UploadFile;
using Zadana.Application.Modules.Orders.Commands.CancelCustomerOrder;
using Zadana.Application.Modules.Orders.Commands.CreateOrderComplaint;
using Zadana.Application.Modules.Orders.Commands.DeleteCustomerOrder;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Queries.GetCustomerOrderComplaint;
using Zadana.Application.Modules.Orders.Queries.GetCustomerOrderDetail;
using Zadana.Application.Modules.Orders.Queries.GetCustomerOrders;
using Zadana.Application.Modules.Orders.Queries.GetCustomerOrderTracking;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.Commands.RetryPaymobPayment;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/orders")]
[Tags("Customer App API")]
[Authorize(Policy = "CustomerOnly")]
public class OrdersController : ApiControllerBase
{
    private const string DeviceIdHeader = "X-Device-Id";
    private readonly ICurrentUserService _currentUserService;
    private readonly IOrderReadService _orderReadService;
    private readonly IOrderSupportCaseWorkflowService _orderSupportCaseWorkflowService;

    public OrdersController(
        ICurrentUserService currentUserService,
        IOrderReadService orderReadService,
        IOrderSupportCaseWorkflowService orderSupportCaseWorkflowService)
    {
        _currentUserService = currentUserService;
        _orderReadService = orderReadService;
        _orderSupportCaseWorkflowService = orderSupportCaseWorkflowService;
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

    [HttpGet("cancellation-reasons")]
    public ActionResult<IReadOnlyList<CustomerOrderCancellationReasonResponse>> GetCancellationReasons()
    {
        var reasons = CustomerOrderCancellationReasonCatalog.GetAll()
            .Select(item => new CustomerOrderCancellationReasonResponse(
                item.Code,
                item.LabelAr,
                item.LabelEn,
                item.RequiresNote))
            .ToList();

        return Ok(reasons);
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
        var result = await Sender.Send(
            new CancelCustomerOrderCommand(orderId, userId, request.ReasonCode, request.Reason, request.Note),
            cancellationToken);

        return Ok(new CancelCustomerOrderResponse(
            result.Message,
            new CancelledOrderStatusResponse(result.Id, result.Status)));
    }

    [HttpPost("{orderId:guid}/retry-payment")]
    public async Task<ActionResult<RetryOrderPaymentResponse>> RetryPayment(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new RetryPaymobPaymentCommand(orderId, userId), cancellationToken);

        return Ok(new RetryOrderPaymentResponse(
            result.Message,
            new CheckoutOrderPaymentResponse(
                result.Payment.Id,
                result.Payment.Provider,
                result.Payment.Status,
                result.Payment.IframeUrl,
                result.Payment.ProviderReference)));
    }

    [HttpDelete("{orderId:guid}")]
    public async Task<ActionResult<DeleteCustomerOrderResponse>> DeleteOrder(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new DeleteCustomerOrderCommand(orderId, userId), cancellationToken);

        return Ok(new DeleteCustomerOrderResponse(result.Message, result.OrderId, true));
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

    [HttpPost("{orderId:guid}/cases")]
    public async Task<ActionResult<CreateOrderSupportCaseResponse>> CreateSupportCase(
        Guid orderId,
        [FromBody] CreateOrderSupportCaseRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var supportCase = await _orderSupportCaseWorkflowService.CreateCustomerCaseAsync(
            orderId,
            userId,
            request.Type,
            request.ReasonCode,
            request.Message,
            request.Attachments?.Select(item => new OrderSupportCaseAttachmentInput(item.FileName, item.FileUrl)).ToList(),
            cancellationToken);

        var result = await _orderReadService.GetCustomerOrderSupportCaseAsync(orderId, supportCase.Id, userId, cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", supportCase.Id);

        return Ok(new CreateOrderSupportCaseResponse("support case submitted successfully", MapSupportCase(result)));
    }

    [HttpGet("{orderId:guid}/cases")]
    public async Task<ActionResult<GetOrderSupportCasesResponse>> GetSupportCases(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await _orderReadService.GetCustomerOrderSupportCasesAsync(orderId, userId, cancellationToken);
        return Ok(new GetOrderSupportCasesResponse(result.Select(MapSupportCase).ToList()));
    }

    [HttpGet("{orderId:guid}/cases/{caseId:guid}")]
    public async Task<ActionResult<GetOrderSupportCaseResponse>> GetSupportCase(
        Guid orderId,
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await _orderReadService.GetCustomerOrderSupportCaseAsync(orderId, caseId, userId, cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);

        return Ok(new GetOrderSupportCaseResponse(MapSupportCase(result)));
    }

    [HttpPost("{orderId:guid}/cases/attachments")]
    public async Task<ActionResult<OrderSupportCaseAttachmentUploadResponse>> UploadSupportCaseAttachment(
        Guid orderId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new BadRequestException("INVALID_FILE", "File is empty.");
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var order = await _orderReadService.GetCustomerOrderDetailAsync(orderId, userId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        await using var stream = file.OpenReadStream();
        var fileDto = new FileUploadDto(file.FileName, file.ContentType, stream);
        var fileUrl = await Sender.Send(new UploadFileCommand($"orders/support-cases/{order.Id:D}", fileDto), cancellationToken);

        return Ok(new OrderSupportCaseAttachmentUploadResponse(file.FileName, fileUrl));
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
                request.EffectiveVendorId,
                request.EffectiveAddressId,
                request.EffectiveDeliverySlotId,
                request.EffectivePaymentMethod,
                request.EffectivePromoCode,
                request.EffectiveNotes,
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
            dto.PaymentStatus,
            dto.PaymentMethod,
            dto.CanRetryPayment,
            dto.CanDelete,
            dto.CanCancel,
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
            dto.PaymentStatus,
            dto.PaymentMethod,
            dto.CanRetryPayment,
            dto.CanDelete,
            dto.CanCancel,
            dto.ItemsCount,
            new CustomerOrderSummaryResponse(
                dto.Summary.Subtotal,
                dto.Summary.ShippingCost,
                dto.Summary.Total),
            dto.Items.Select(MapOrderProduct).ToList(),
            MapSupportCaseSummary(dto.ActiveCase));

    private static CustomerOrderTrackingResponse MapOrderTracking(CustomerOrderTrackingDto dto) =>
        new(
            new CustomerOrderTrackingOrderResponse(dto.Order.Id, dto.Order.Status),
            dto.EstimatedDelivery is null
                ? null
                : new CustomerOrderEstimatedDeliveryResponse(dto.EstimatedDelivery.Datetime, dto.EstimatedDelivery.Formatted),
            dto.Driver is null
                ? null
                : new CustomerOrderTrackingDriverResponse(dto.Driver.Id, dto.Driver.Name, dto.Driver.PhoneNumber, dto.Driver.Subtitle),
            dto.AssignedDriver is null
                ? null
                : new CustomerAssignedDriverResponse(
                    dto.AssignedDriver.Id,
                    dto.AssignedDriver.Name,
                    dto.AssignedDriver.PhoneNumber,
                    dto.AssignedDriver.VehicleType,
                    dto.AssignedDriver.PlateNumber),
            dto.DriverArrivalState,
            dto.DriverArrivalUpdatedAtUtc,
            dto.DeliveryOtp,
            dto.ShowDeliveryOtp,
            MapSupportCaseSummary(dto.ActiveCase),
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

    private static OrderSupportCaseSummaryResponse? MapSupportCaseSummary(OrderSupportCaseSummaryDto? dto) =>
        dto is null
            ? null
            : new OrderSupportCaseSummaryResponse(
                dto.Id,
                dto.Type,
                dto.Status,
                dto.Queue,
                dto.Priority,
                dto.ReasonCode,
                dto.Message,
                dto.CreatedAt,
                dto.UpdatedAt);

    private static OrderSupportCaseResponse MapSupportCase(OrderSupportCaseDto dto) =>
        new(
            dto.Id,
            dto.OrderId,
            dto.Type,
            dto.Status,
            dto.Queue,
            dto.Priority,
            dto.ReasonCode,
            dto.Message,
            dto.CustomerVisibleNote,
            dto.DecisionNotes,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.SlaDueAtUtc,
            dto.RequestedRefundAmount,
            dto.ApprovedRefundAmount,
            dto.RefundMethod,
            dto.CostBearer,
            dto.Attachments
                .Select(item => new OrderSupportCaseAttachmentResponse(item.FileName, item.FileUrl))
                .ToList(),
            dto.Activities
                .Select(item => new OrderSupportCaseActivityResponse(
                    item.Action,
                    item.Title,
                    item.Note,
                    item.ActorRole,
                    item.VisibleToCustomer,
                    item.CreatedAt))
                .ToList());

    private string? ResolveDeviceIdHeader()
    {
        var deviceId = Request.Headers[DeviceIdHeader].ToString();
        return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
    }
}
