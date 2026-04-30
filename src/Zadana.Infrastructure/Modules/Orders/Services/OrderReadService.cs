using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Orders.Services;

public class OrderReadService : IOrderReadService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDriverCommitmentPolicyService _driverCommitmentPolicyService;

    public OrderReadService(ApplicationDbContext dbContext, IDriverCommitmentPolicyService driverCommitmentPolicyService)
    {
        _dbContext = dbContext;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
    }

    public OrderReadService(ApplicationDbContext dbContext)
        : this(dbContext, new DriverCommitmentPolicyService(dbContext, dbContext))
    {
    }

    public Task<OrderDto?> GetByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Id == orderId && order.UserId == userId)
            .Select(order => new OrderDto(
                order.Id,
                order.OrderNumber,
                order.UserId,
                order.VendorId,
                order.CustomerAddressId,
                order.Status.ToString(),
                order.PaymentMethod.ToString(),
                order.PaymentStatus.ToString(),
                order.Subtotal,
                order.DeliveryFee,
                order.TotalAmount,
                order.PlacedAtUtc,
                order.Items.Select(item => new OrderItemDto(
                    item.Id,
                    item.VendorProductId,
                    item.MasterProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice,
                    item.LineTotal)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CustomerOrderListDto> GetCustomerOrdersAsync(
        Guid userId,
        CustomerOrderBucket bucket,
        int page,
        int perPage,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPerPage = perPage <= 0 ? 20 : perPage;

        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId == userId);

        query = bucket switch
        {
            CustomerOrderBucket.Completed => query.Where(order =>
                order.Status == OrderStatus.Delivered ||
                order.Status == OrderStatus.Cancelled ||
                order.Status == OrderStatus.VendorRejected ||
                order.Status == OrderStatus.DeliveryFailed),
            CustomerOrderBucket.Returns => query.Where(order => order.Status == OrderStatus.Refunded),
            _ => query.Where(order =>
                order.Status != OrderStatus.Delivered &&
                order.Status != OrderStatus.Refunded &&
                order.Status != OrderStatus.Cancelled &&
                order.Status != OrderStatus.VendorRejected &&
                order.Status != OrderStatus.DeliveryFailed)
        };

        var total = await query.CountAsync(cancellationToken);
        var orders = await query
            .Include(order => order.Items)
            .OrderByDescending(order => order.PlacedAtUtc)
            .Skip((normalizedPage - 1) * normalizedPerPage)
            .Take(normalizedPerPage)
            .ToListAsync(cancellationToken);

        var items = orders.Select(MapListItem).ToList();

        return new CustomerOrderListDto(items, normalizedPage, normalizedPerPage, total);
    }

    public async Task<CustomerOrderDetailDto?> GetCustomerOrderDetailAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.SupportCases)
            .Where(order => order.Id == orderId && order.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return order is null ? null : MapDetail(order);
    }

    public async Task<CustomerOrderTrackingDto?> GetCustomerOrderTrackingAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(x => x.StatusHistory)
            .Include(x => x.SupportCases)
            .Where(x => x.Id == orderId && x.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        var assignment = await _dbContext.DeliveryAssignments
            .AsNoTracking()
            .Include(x => x.Driver)
            .ThenInclude(x => x!.User)
            .Where(x =>
                x.OrderId == order.Id &&
                x.DriverId != null &&
                x.Status != AssignmentStatus.SearchingDriver &&
                x.Status != AssignmentStatus.OfferSent &&
                x.Status != AssignmentStatus.Rejected)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var timeline = BuildTimeline(order);
        var estimatedDelivery = await BuildEstimatedDeliveryAsync(order, assignment, cancellationToken);
        var driver = BuildDriver(assignment);
        var assignedDriver = BuildAssignedDriverSummary(assignment);
        var arrivalState = ResolveArrivalState(assignment);
        var arrivalUpdatedAtUtc = ResolveArrivalUpdatedAtUtc(assignment);
        var showDeliveryOtp = (order.Status == OrderStatus.PickedUp || order.Status == OrderStatus.OnTheWay) &&
            assignment is not null &&
            !assignment.DeliveryOtpVerifiedAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(assignment.DeliveryOtpCode);

        return new CustomerOrderTrackingDto(
            new CustomerOrderTrackingOrderDto(order.Id, MapTrackingStatus(order.Status)),
            estimatedDelivery,
            driver,
            assignedDriver,
            arrivalState,
            arrivalUpdatedAtUtc,
            showDeliveryOtp ? assignment!.DeliveryOtpCode : null,
            showDeliveryOtp,
            ResolveActiveSupportCaseSummary(order.SupportCases),
            timeline);
    }

    public async Task<IReadOnlyList<OrderSupportCaseDto>> GetCustomerOrderSupportCasesAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Orders
            .AsNoTracking()
            .AnyAsync(order => order.Id == orderId && order.UserId == userId, cancellationToken);

        if (!exists)
        {
            return [];
        }

        var items = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(item => item.Attachments)
            .Include(item => item.Activities)
            .Where(item => item.OrderId == orderId && item.CustomerUserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return items.Select(MapSupportCase).ToList();
    }

    public async Task<OrderSupportCaseDto?> GetCustomerOrderSupportCaseAsync(
        Guid orderId,
        Guid caseId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(item => item.Attachments)
            .Include(item => item.Activities)
            .Where(item => item.OrderId == orderId && item.Id == caseId && item.CustomerUserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return supportCase is null ? null : MapSupportCase(supportCase);
    }

    public async Task<OrderComplaintDto?> GetCustomerOrderComplaintAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(item => item.Attachments)
            .Where(item => item.OrderId == orderId && item.CustomerUserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (supportCase is not null)
        {
            return MapLegacyComplaint(supportCase);
        }

        var complaint = await _dbContext.OrderComplaints
            .AsNoTracking()
            .Include(item => item.Attachments)
            .Where(item => item.OrderId == orderId && item.Order.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return complaint is null ? null : MapComplaint(complaint);
    }

    public Task<PaginatedList<AdminVendorOrderListItemDto>> GetVendorOrdersAsync(
        Guid vendorId,
        string? search,
        string? status,
        string? paymentStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.VendorId == vendorId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(order =>
                order.OrderNumber.ToLower().Contains(normalizedSearch) ||
                order.User.FullName.ToLower().Contains(normalizedSearch) ||
                (order.User.PhoneNumber != null && order.User.PhoneNumber.ToLower().Contains(normalizedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(order => order.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus) && Enum.TryParse<PaymentStatus>(paymentStatus, true, out var parsedPaymentStatus))
        {
            query = query.Where(order => order.PaymentStatus == parsedPaymentStatus);
        }

        var projected = query
            .OrderByDescending(order => order.PlacedAtUtc)
            .Select(order => new AdminVendorOrderListItemDto(
                order.Id,
                order.OrderNumber,
                order.VendorId,
                order.UserId,
                order.User.FullName,
                order.Status.ToString(),
                order.PaymentStatus.ToString(),
                order.Subtotal,
                order.DeliveryFee,
                order.CommissionAmount,
                order.TotalAmount,
                order.Items.Count,
                order.PlacedAtUtc));

        return PaginatedList<AdminVendorOrderListItemDto>.CreateAsync(projected, page, pageSize, cancellationToken);
    }

    public Task<PaginatedList<VendorOrderListItemDto>> GetVendorWorkspaceOrdersAsync(
        Guid vendorId,
        string? search,
        string? status,
        string? paymentMethod,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.VendorId == vendorId && order.Status != OrderStatus.PendingPayment);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(order =>
                order.OrderNumber.ToLower().Contains(normalizedSearch) ||
                order.User.FullName.ToLower().Contains(normalizedSearch) ||
                (order.User.PhoneNumber != null && order.User.PhoneNumber.ToLower().Contains(normalizedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(order => order.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod) && Enum.TryParse<Zadana.Domain.Modules.Payments.Enums.PaymentMethodType>(paymentMethod, true, out var parsedPaymentMethod))
        {
            query = query.Where(order => order.PaymentMethod == parsedPaymentMethod);
        }

        query = query.OrderByDescending(order => order.PlacedAtUtc);

        var projected = query.Select(order => new VendorOrderListItemDto(
            order.Id,
            order.OrderNumber,
            order.User.FullName,
            order.User.PhoneNumber ?? string.Empty,
            order.Status.ToString(),
            order.PaymentStatus.ToString(),
            order.PaymentMethod.ToString(),
            order.TotalAmount,
            order.Items.Count,
            order.PlacedAtUtc,
            IsLate(order.Status, order.PlacedAtUtc)));

        return PaginatedList<VendorOrderListItemDto>.CreateAsync(projected, page, pageSize, cancellationToken);
    }

    public async Task<VendorOrderDetailDto?> GetVendorOrderDetailAsync(
        Guid vendorId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Items)
            .Include(item => item.StatusHistory)
            .Include(item => item.Vendor)
            .Where(item => item.VendorId == vendorId && item.Id == orderId && item.Status != OrderStatus.PendingPayment)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        var customerAddress = await _dbContext.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.Id == order.CustomerAddressId)
            .Select(address => new
            {
                address.AddressLine,
                address.City,
                address.Area,
                address.ContactPhone,
                address.Latitude,
                address.Longitude
            })
            .FirstOrDefaultAsync(cancellationToken);

        var customerAddressText = customerAddress is null
            ? string.Empty
            : string.Join(", ", new[] { customerAddress.AddressLine, customerAddress.Area, customerAddress.City }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        var assignment = await _dbContext.DeliveryAssignments
            .AsNoTracking()
            .Include(item => item.Driver)
                .ThenInclude(driver => driver!.User)
            .Where(item =>
                item.OrderId == order.Id &&
                item.DriverId != null &&
                item.Status != AssignmentStatus.SearchingDriver &&
                item.Status != AssignmentStatus.OfferSent &&
                item.Status != AssignmentStatus.Rejected)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var assignedDriver = BuildAssignedDriverSummary(assignment);
        var arrivalState = ResolveArrivalState(assignment);
        var arrivalUpdatedAtUtc = ResolveArrivalUpdatedAtUtc(assignment);
        var canConfirmPickup = order.Status == OrderStatus.DriverAssigned &&
            assignment is not null &&
            (assignment.Status == AssignmentStatus.Accepted ||
             assignment.Status == AssignmentStatus.ArrivedAtVendor) &&
            !assignment.PickupOtpVerifiedAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(assignment.PickupOtpCode);
        var pickupOtpStatus = assignment is null || string.IsNullOrWhiteSpace(assignment.PickupOtpCode)
            ? "not_available"
            : assignment.PickupOtpVerifiedAtUtc.HasValue
                ? "verified"
                : "pending";

        // â”€â”€ Live Tracking: vendor, customer, and driver locations â”€â”€
        GeoPointDto? vendorLocation = null;
        if (order.VendorBranchId.HasValue)
        {
            var branch = await _dbContext.Set<VendorBranch>()
                .AsNoTracking()
                .Where(b => b.Id == order.VendorBranchId.Value)
                .Select(b => new { b.Latitude, b.Longitude })
                .FirstOrDefaultAsync(cancellationToken);
            if (branch is not null)
                vendorLocation = new GeoPointDto(branch.Latitude, branch.Longitude);
        }

        GeoPointDto? customerLocation = null;
        if (customerAddress is { Latitude: not null, Longitude: not null })
        {
            customerLocation = new GeoPointDto(customerAddress.Latitude.Value, customerAddress.Longitude.Value);
        }

        DriverLiveLocationDto? driverLiveLocation = null;
        if (assignment?.DriverId != null && IsActiveDeliveryStatus(order.Status))
        {
            var latestLocation = await _dbContext.DriverLocations
                .AsNoTracking()
                .Where(l => l.DriverId == assignment.DriverId.Value)
                .OrderByDescending(l => l.RecordedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (latestLocation is not null)
            {
                driverLiveLocation = new DriverLiveLocationDto(
                    latestLocation.Latitude,
                    latestLocation.Longitude,
                    latestLocation.AccuracyMeters,
                    latestLocation.RecordedAtUtc);
            }
        }

        return new VendorOrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.User.FullName,
            customerAddress?.ContactPhone ?? order.User.PhoneNumber ?? string.Empty,
            customerAddressText,
            order.Status.ToString(),
            order.PaymentStatus.ToString(),
            order.PaymentMethod.ToString(),
            order.Subtotal,
            order.DeliveryFee,
            order.TotalAmount,
            order.Notes,
            order.PlacedAtUtc,
            assignedDriver,
            arrivalState,
            arrivalUpdatedAtUtc,
            null, // Deprecated: pickup OTP code is never shown to vendor; vendor inputs code from driver
            canConfirmPickup,
            pickupOtpStatus,
            vendorLocation,
            customerLocation,
            driverLiveLocation,
            order.Items.Select(item => new OrderItemDto(
                item.Id,
                item.VendorProductId,
                item.MasterProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.LineTotal)).ToList(),
            BuildVendorTimeline(order));
    }

    public async Task<AdminOrdersListDto> GetAdminOrdersAsync(
        string? search,
        string? status,
        string? paymentStatus,
        string? fulfillmentStatus,
        string? queueView,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 10 : pageSize;

        // â”€â”€ 1. Build IQueryable with server-side filters â”€â”€
        var query = _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.User)
            .Include(order => order.Vendor)
            .Include(order => order.VendorBranch)
            .Include(order => order.SupportCases)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(order =>
                order.OrderNumber.ToLower().Contains(s) ||
                order.User.FullName.ToLower().Contains(s) ||
                (order.User.PhoneNumber != null && order.User.PhoneNumber.ToLower().Contains(s)) ||
                order.Vendor.BusinessNameAr.ToLower().Contains(s) ||
                order.Vendor.BusinessNameEn.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<OrderStatus>(status, true, out var ps))
                query = query.Where(order => order.Status == ps);
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus) && !string.Equals(paymentStatus, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<PaymentStatus>(paymentStatus, true, out var pps))
                query = query.Where(order => order.PaymentStatus == pps);
        }

        if (!string.IsNullOrWhiteSpace(queueView) && !string.Equals(queueView, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-45);
            query = queueView.ToUpperInvariant() switch
            {
                "ACTIVE" => query.Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Delivered),
                "LATE" => query.Where(o => o.PlacedAtUtc < cutoff && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Delivered),
                "PAYMENT_ISSUES" => query.Where(o => o.PaymentStatus == PaymentStatus.Failed || o.PaymentStatus == PaymentStatus.Pending || o.PaymentStatus == PaymentStatus.PendingCollection),
                "REFUNDS" => query.Where(o => o.PaymentStatus == PaymentStatus.Refunded || o.PaymentStatus == PaymentStatus.PartiallyRefunded),
                _ => query
            };
        }

        // â”€â”€ 2. Count + KPI summary â”€â”€
        var totalCount = await query.CountAsync(cancellationToken);

        var allOrders = _dbContext.Orders.AsNoTracking();
        var cutoffKpi = DateTime.UtcNow.AddMinutes(-45);
        var kpi = await allOrders
            .GroupBy(o => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Delivered),
                Late = g.Count(o => o.PlacedAtUtc < cutoffKpi && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Delivered),
                PayIssues = g.Count(o => o.PaymentStatus == PaymentStatus.Failed || o.PaymentStatus == PaymentStatus.Pending || o.PaymentStatus == PaymentStatus.PendingCollection),
                Refunds = g.Count(o => o.PaymentStatus == PaymentStatus.Refunded || o.PaymentStatus == PaymentStatus.PartiallyRefunded)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var summary = new AdminOrdersSummaryDto(kpi?.Total ?? 0, kpi?.Active ?? 0, kpi?.Late ?? 0, kpi?.PayIssues ?? 0, kpi?.Refunds ?? 0);

        // â”€â”€ 3. Paginate in SQL â”€â”€
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)normalizedPageSize));
        var safePage = Math.Min(normalizedPage, totalPages);

        var pagedOrders = await query
            .OrderByDescending(order => order.PlacedAtUtc)
            .Skip((safePage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        if (pagedOrders.Count == 0)
        {
            return new AdminOrdersListDto([], safePage, normalizedPageSize, totalCount, totalPages, safePage > 1, safePage < totalPages, summary);
        }

        // â”€â”€ 4. Load related data only for the page â”€â”€
        var ids = pagedOrders.Select(o => o.Id).ToList();
        var addressMap = await LoadAddressMapAsync(ids, cancellationToken);
        var paymentMap = await LoadPaymentMapAsync(ids, cancellationToken);
        var refundMap = await LoadRefundMapAsync(ids, cancellationToken);
        var assignmentMap = await LoadAssignmentMapAsync(ids, cancellationToken);

        var items = pagedOrders
            .Select(order => BuildAdminOrderProjection(order, addressMap.GetValueOrDefault(order.Id), paymentMap.GetValueOrDefault(order.Id), refundMap.GetValueOrDefault(order.Id), assignmentMap.GetValueOrDefault(order.Id)).ListItem)
            .ToList();

        return new AdminOrdersListDto(items, safePage, normalizedPageSize, totalCount, totalPages, safePage > 1, safePage < totalPages, summary);
    }

    public async Task<AdminOrderDetailDto?> GetAdminOrderDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Items)
            .Include(item => item.StatusHistory)
            .Include(item => item.Vendor)
            .Include(item => item.VendorBranch)
            .Include(item => item.SupportCases)
            .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var address = await LoadAddressMapAsync([order.Id], cancellationToken);
        var payment = await LoadPaymentMapAsync([order.Id], cancellationToken);
        var refunds = await LoadRefundMapAsync([order.Id], cancellationToken);
        var assignments = await LoadAssignmentMapAsync([order.Id], cancellationToken);
        var driverCandidates = await LoadDriverCandidatesAsync(order, cancellationToken);

        return BuildAdminOrderDetail(
            order,
            address.GetValueOrDefault(order.Id),
            payment.GetValueOrDefault(order.Id),
            refunds.GetValueOrDefault(order.Id),
            assignments.GetValueOrDefault(order.Id),
            driverCandidates);
    }

    public async Task<AdminOrderSupportCasesListDto> GetAdminOrderSupportCasesAsync(
        string? search,
        string? type,
        string? status,
        string? priority,
        string? queue,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 20 : pageSize;

        var cases = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(item => item.Order)
                .ThenInclude(order => order.User)
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .Include(item => item.Attachments)
            .Include(item => item.Activities)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var orderIds = cases.Select(item => item.OrderId).Distinct().ToList();
        var paymentMap = await LoadPaymentMapAsync(orderIds, cancellationToken);
        var refundMap = await LoadRefundMapAsync(orderIds, cancellationToken);

        var projected = cases
            .Select(item => BuildAdminSupportCaseListItem(
                item,
                paymentMap.GetValueOrDefault(item.OrderId),
                refundMap.GetValueOrDefault(item.OrderId)))
            .Where(item => MatchesAdminSupportCaseFilters(item, search, type, status, priority, queue))
            .ToList();

        var totalCount = projected.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)normalizedPageSize));
        var safePage = Math.Min(normalizedPage, totalPages);
        var paged = projected
            .Skip((safePage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return new AdminOrderSupportCasesListDto(
            paged,
            safePage,
            normalizedPageSize,
            totalCount,
            totalPages,
            safePage > 1,
            safePage < totalPages);
    }

    public async Task<AdminOrderSupportCaseListItemDto?> GetAdminOrderSupportCaseDetailAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(item => item.Order)
                .ThenInclude(order => order.User)
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .Include(item => item.Attachments)
            .Include(item => item.Activities)
            .FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);

        if (supportCase is null)
        {
            return null;
        }

        var payment = await _dbContext.Payments
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderId == supportCase.OrderId, cancellationToken);

        var refunds = await _dbContext.Refunds
            .AsNoTracking()
            .Where(item => item.Payment.OrderId == supportCase.OrderId)
            .ToListAsync(cancellationToken);

        return BuildAdminSupportCaseListItem(supportCase, payment, refunds);
    }

    private static CustomerOrderListItemDto MapListItem(Order order) =>
        new(
            order.Id,
            order.PlacedAtUtc,
            order.TotalAmount,
            MapStatus(order.Status),
            MapCustomerPaymentStatus(order.PaymentStatus),
            MapCustomerPaymentMethod(order.PaymentMethod),
            CanRetryPayment(order),
            CanDelete(order),
            CanCancel(order.Status),
            order.Items.Count,
            order.Items
                .Select(item => new CustomerOrderProductDto(
                    item.Id,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice))
                .ToList());

    private static CustomerOrderDetailDto MapDetail(Order order) =>
        new(
            order.Id,
            order.PlacedAtUtc,
            order.TotalAmount,
            MapStatus(order.Status),
            MapCustomerPaymentStatus(order.PaymentStatus),
            MapCustomerPaymentMethod(order.PaymentMethod),
            CanRetryPayment(order),
            CanDelete(order),
            CanCancel(order.Status),
            order.Items.Count,
            new CustomerOrderPriceSummaryDto(
                order.Subtotal,
                order.DeliveryFee,
                order.TotalAmount),
            order.Items
                .Select(item => new CustomerOrderProductDto(
                    item.Id,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice))
                .ToList(),
            ResolveActiveSupportCaseSummary(order.SupportCases));

    private static OrderComplaintDto MapComplaint(OrderComplaint complaint) =>
        new(
            complaint.Id,
            MapComplaintStatus(complaint.Status),
            complaint.Message,
            complaint.Attachments
                .Select(attachment => new OrderComplaintAttachmentDto(
                    attachment.FileName,
                    attachment.FileUrl))
                .ToList(),
            complaint.CreatedAtUtc);

    private static OrderComplaintDto MapLegacyComplaint(OrderSupportCase supportCase) =>
        new(
            supportCase.Id,
            MapSupportCaseStatus(supportCase.Status),
            supportCase.Message,
            supportCase.Attachments
                .Select(attachment => new OrderComplaintAttachmentDto(
                    attachment.FileName,
                    attachment.FileUrl))
                .ToList(),
            supportCase.CreatedAtUtc);

    private static OrderSupportCaseDto MapSupportCase(OrderSupportCase supportCase) =>
        new(
            supportCase.Id,
            supportCase.OrderId,
            MapSupportCaseType(supportCase.Type),
            MapSupportCaseStatus(supportCase.Status),
            MapSupportCaseQueue(supportCase.Queue),
            MapSupportCasePriority(supportCase.Priority),
            supportCase.ReasonCode,
            supportCase.Message,
            supportCase.CustomerVisibleNote,
            supportCase.DecisionNotes,
            supportCase.CreatedAtUtc,
            supportCase.UpdatedAtUtc,
            supportCase.SlaDueAtUtc,
            supportCase.RequestedRefundAmount,
            supportCase.ApprovedRefundAmount,
            supportCase.RefundMethod,
            supportCase.CostBearer,
            supportCase.Attachments
                .Select(attachment => new OrderSupportCaseAttachmentDto(
                    attachment.FileName,
                    attachment.FileUrl))
                .ToList(),
            supportCase.Activities
                .OrderByDescending(activity => activity.CreatedAtUtc)
                .Select(activity => new OrderSupportCaseActivityDto(
                    activity.Action,
                    activity.Title,
                    activity.Note,
                    activity.ActorRole,
                    activity.VisibleToCustomer,
                    activity.CreatedAtUtc))
                .ToList());

    private static OrderSupportCaseSummaryDto? ResolveActiveSupportCaseSummary(IEnumerable<OrderSupportCase> supportCases)
    {
        var supportCase = supportCases
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault(item => item.Status != OrderSupportCaseStatus.Rejected && item.Status != OrderSupportCaseStatus.Resolved)
            ?? supportCases.OrderByDescending(item => item.CreatedAtUtc).FirstOrDefault();

        return supportCase is null
            ? null
            : new OrderSupportCaseSummaryDto(
                supportCase.Id,
                MapSupportCaseType(supportCase.Type),
                MapSupportCaseStatus(supportCase.Status),
                MapSupportCaseQueue(supportCase.Queue),
                MapSupportCasePriority(supportCase.Priority),
                supportCase.ReasonCode,
                supportCase.Message,
                supportCase.CreatedAtUtc,
                supportCase.UpdatedAtUtc);
    }

    private static List<CustomerOrderTrackingTimelineItemDto> BuildTimeline(Order order)
    {
        var history = order.StatusHistory
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        var isCancelled = order.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed;
        var isReturning = order.Status == OrderStatus.Refunded;
        var terminalId = isCancelled ? "cancelled" : isReturning ? "returning" : "delivered";
        var terminalTitle = isCancelled ? "Order cancelled" : isReturning ? "Return in progress" : "Delivered";

        var steps = new List<TrackingStepDefinition>
        {
            new("order_placed", "Order placed", GetStepTime(order.PlacedAtUtc), IsCurrentStage(order.Status, TrackingStage.OrderPlaced), IsCompletedStage(order.Status, TrackingStage.OrderPlaced)),
            new("vendor_confirmed", "Vendor confirmed", GetStepTime(ResolveStepDate(history, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.DriverAssignmentInProgress, OrderStatus.DriverAssigned, OrderStatus.PickedUp, OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Refunded)), IsCurrentStage(order.Status, TrackingStage.VendorConfirmed), IsCompletedStage(order.Status, TrackingStage.VendorConfirmed)),
            new("preparing", "Preparing order", GetStepTime(ResolveStepDate(history, OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.DriverAssignmentInProgress, OrderStatus.DriverAssigned, OrderStatus.PickedUp, OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Refunded)), IsCurrentStage(order.Status, TrackingStage.Preparing), IsCompletedStage(order.Status, TrackingStage.Preparing)),
            new("out_for_delivery", "Out for delivery", GetStepTime(ResolveStepDate(history, OrderStatus.PickedUp, OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Refunded)), IsCurrentStage(order.Status, TrackingStage.OutForDelivery), IsCompletedStage(order.Status, TrackingStage.OutForDelivery))
        };

        var terminalTime = terminalId switch
        {
            "cancelled" => GetStepTime(order.CancelledAtUtc ?? ResolveStepDate(history, OrderStatus.Cancelled, OrderStatus.VendorRejected, OrderStatus.DeliveryFailed)),
            "returning" => GetStepTime(ResolveStepDate(history, OrderStatus.Refunded)),
            _ => GetStepTime(order.DeliveredAtUtc ?? ResolveStepDate(history, OrderStatus.Delivered))
        };

        steps.Add(new TrackingStepDefinition(
            terminalId,
            terminalTitle,
            terminalTime,
            IsTerminalActive(order.Status),
            IsTerminalCompleted(order.Status)));

        return steps
            .Select(step => new CustomerOrderTrackingTimelineItemDto(
                step.Id,
                step.Title,
                step.Time,
                step.IsActive,
                step.IsCompleted))
            .ToList();
    }

    private async Task<CustomerOrderEstimatedDeliveryDto?> BuildEstimatedDeliveryAsync(
        Order order, DeliveryAssignment? assignment, CancellationToken cancellationToken)
    {
        if (order.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded)
        {
            return null;
        }

        // For delivered orders, just show the actual delivery time
        if (order.Status == OrderStatus.Delivered)
        {
            var deliveredAt = order.DeliveredAtUtc ?? ResolveHistoryDate(order, OrderStatus.Delivered) ?? order.PlacedAtUtc;
            return new CustomerOrderEstimatedDeliveryDto(
                deliveredAt,
                deliveredAt.ToString("dd MMM yyyy, hh:mm tt 'UTC'", CultureInfo.InvariantCulture));
        }

        // Calculate average delivery time from vendor's past orders (last 30 days, max 50)
        var avgMinutes = await CalculateVendorAverageDeliveryMinutesAsync(order.VendorId, cancellationToken);

        var estimatedAtUtc = order.Status switch
        {
            OrderStatus.OnTheWay => (assignment?.PickedUpAtUtc ?? assignment?.AcceptedAtUtc ?? order.PlacedAtUtc).AddMinutes(avgMinutes * 0.4),
            OrderStatus.PickedUp => (assignment?.PickedUpAtUtc ?? order.PlacedAtUtc).AddMinutes(avgMinutes * 0.5),
            OrderStatus.DriverAssigned => (assignment?.AcceptedAtUtc ?? order.PlacedAtUtc).AddMinutes(avgMinutes * 0.8),
            OrderStatus.DriverAssignmentInProgress => order.PlacedAtUtc.AddMinutes(avgMinutes),
            _ => order.PlacedAtUtc.AddMinutes(avgMinutes)
        };

        // Ensure the estimate is never in the past for active orders
        if (estimatedAtUtc < DateTime.UtcNow)
        {
            estimatedAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, avgMinutes * 0.2));
        }

        return new CustomerOrderEstimatedDeliveryDto(
            estimatedAtUtc,
            estimatedAtUtc.ToString("dd MMM yyyy, hh:mm tt 'UTC'", CultureInfo.InvariantCulture));
    }

    private async Task<double> CalculateVendorAverageDeliveryMinutesAsync(
        Guid vendorId, CancellationToken cancellationToken)
    {
        const double defaultMinutes = 35.0;
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var deliveredOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o =>
                o.VendorId == vendorId &&
                o.Status == OrderStatus.Delivered &&
                o.DeliveredAtUtc.HasValue &&
                o.PlacedAtUtc >= cutoff)
            .OrderByDescending(o => o.DeliveredAtUtc)
            .Take(50)
            .Select(o => new { o.PlacedAtUtc, o.DeliveredAtUtc })
            .ToArrayAsync(cancellationToken);

        if (deliveredOrders.Length < 3)
        {
            return defaultMinutes;
        }

        var avgMinutes = deliveredOrders
            .Average(o => (o.DeliveredAtUtc!.Value - o.PlacedAtUtc).TotalMinutes);

        // Clamp to reasonable range: 10 min minimum, 120 min maximum
        return Math.Clamp(avgMinutes, 10.0, 120.0);
    }

    private static CustomerOrderTrackingDriverDto? BuildDriver(DeliveryAssignment? assignment)
    {
        if (assignment?.Driver?.User is null)
        {
            return null;
        }

        return new CustomerOrderTrackingDriverDto(
            assignment.Driver.Id,
            assignment.Driver.User.FullName,
            assignment.Driver.User.PhoneNumber,
            assignment.Driver.VehicleType?.ToString() ?? "Delivery Driver");
    }

    private static AssignedDriverSummaryDto? BuildAssignedDriverSummary(DeliveryAssignment? assignment)
    {
        if (assignment?.Driver?.User is null)
        {
            return null;
        }

        return new AssignedDriverSummaryDto(
            assignment.Driver.Id,
            assignment.Driver.User.FullName,
            assignment.Driver.User.PhoneNumber,
            assignment.Driver.VehicleType?.ToString() ?? "Unknown",
            string.IsNullOrWhiteSpace(assignment.Driver.LicenseNumber) ? "N/A" : assignment.Driver.LicenseNumber);
    }

    private static string ResolveArrivalState(DeliveryAssignment? assignment) =>
        assignment?.Status switch
        {
            AssignmentStatus.ArrivedAtVendor => "arrived_at_vendor",
            AssignmentStatus.ArrivedAtCustomer => "arrived_at_customer",
            _ => "none"
        };

    private static DateTime? ResolveArrivalUpdatedAtUtc(DeliveryAssignment? assignment) =>
        assignment?.Status switch
        {
            AssignmentStatus.ArrivedAtVendor => assignment.ArrivedAtVendorAtUtc,
            AssignmentStatus.ArrivedAtCustomer => assignment.ArrivedAtCustomerAtUtc,
            _ => null
        };

    private static string MapStatus(OrderStatus status) =>
        status switch
        {
            OrderStatus.PendingPayment or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => "pending",
            OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.ReadyForPickup or
            OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or
            OrderStatus.PickedUp or OrderStatus.OnTheWay => "processing",
            OrderStatus.Delivered => "delivered",
            OrderStatus.Refunded => "returning",
            _ => "cancelled"
        };

    private static string MapTrackingStatus(OrderStatus status) =>
        OrderTrackingStatusMapper.ToCustomerTrackingStatus(status);

    private static bool CanCancel(OrderStatus status) =>
        status is OrderStatus.PendingVendorAcceptance or
            OrderStatus.Accepted or
            OrderStatus.Preparing;

    private static bool CanRetryPayment(Order order) =>
        order.PaymentMethod == PaymentMethodType.Card &&
        order.Status == OrderStatus.PendingPayment &&
        order.PaymentStatus is PaymentStatus.Initiated or PaymentStatus.Pending or PaymentStatus.Failed;

    private static bool CanDelete(Order order) =>
        order.Status == OrderStatus.PendingPayment &&
        order.PaymentStatus != PaymentStatus.Paid;

    private static string MapCustomerPaymentStatus(PaymentStatus paymentStatus) =>
        paymentStatus switch
        {
            PaymentStatus.Paid => "paid",
            PaymentStatus.Failed => "failed",
            _ => "pending"
        };

    private static string MapCustomerPaymentMethod(PaymentMethodType paymentMethod) =>
        paymentMethod switch
        {
            PaymentMethodType.Card => "card",
            PaymentMethodType.BankTransfer => "bank",
            _ => "cash"
        };

    private static string MapComplaintStatus(OrderComplaintStatus status) =>
        status switch
        {
            OrderComplaintStatus.Submitted => "submitted",
            OrderComplaintStatus.InReview => "in_review",
            OrderComplaintStatus.Resolved => "resolved",
            _ => "submitted"
        };

    private static string MapSupportCaseType(OrderSupportCaseType type) =>
        type switch
        {
            OrderSupportCaseType.ReturnRequest => "return_request",
            _ => "complaint"
        };

    private static string MapSupportCaseStatus(OrderSupportCaseStatus status) =>
        status switch
        {
            OrderSupportCaseStatus.InReview => "in_review",
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "awaiting_customer_evidence",
            _ => status.ToString().ToLowerInvariant()
        };

    private static string MapSupportCaseQueue(OrderSupportCaseQueue queue) =>
        queue.ToString().ToLowerInvariant();

    private static string MapSupportCasePriority(OrderSupportCasePriority priority) =>
        priority.ToString().ToLowerInvariant();

    private static bool IsCurrentStage(OrderStatus status, TrackingStage stage) =>
        stage switch
        {
            TrackingStage.OrderPlaced => status is OrderStatus.PendingPayment or OrderStatus.Placed,
            TrackingStage.VendorConfirmed => status is OrderStatus.PendingVendorAcceptance or OrderStatus.Accepted,
            TrackingStage.Preparing => status is OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned,
            TrackingStage.OutForDelivery => status is OrderStatus.PickedUp or OrderStatus.OnTheWay,
            _ => false
        };

    private static bool IsCompletedStage(OrderStatus status, TrackingStage stage) =>
        stage switch
        {
            TrackingStage.OrderPlaced => status != OrderStatus.PendingPayment,
            TrackingStage.VendorConfirmed => status is OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay or OrderStatus.Delivered or OrderStatus.Refunded or OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed,
            TrackingStage.Preparing => status is OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay or OrderStatus.Delivered or OrderStatus.Refunded,
            TrackingStage.OutForDelivery => status is OrderStatus.OnTheWay or OrderStatus.Delivered or OrderStatus.Refunded,
            _ => false
        };

    private static bool IsTerminalActive(OrderStatus status) =>
        status is OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded;

    private static bool IsTerminalCompleted(OrderStatus status) =>
        status == OrderStatus.Delivered;

    private static DateTime? ResolveStepDate(IReadOnlyCollection<OrderStatusHistory> history, params OrderStatus[] statuses) =>
        history
            .Where(x => statuses.Contains(x.NewStatus))
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => (DateTime?)x.CreatedAtUtc)
            .FirstOrDefault();

    private static DateTime? ResolveHistoryDate(Order order, params OrderStatus[] statuses) =>
        ResolveStepDate(order.StatusHistory.ToList(), statuses);

    private static string GetStepTime(DateTime? dateTimeUtc) =>
        dateTimeUtc.HasValue
            ? dateTimeUtc.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture)
            : string.Empty;

    private static IReadOnlyList<VendorOrderTimelineItemDto> BuildVendorTimeline(Order order)
    {
        var timeline = new List<VendorOrderTimelineItemDto>
        {
            new(
                OrderStatus.PendingVendorAcceptance.ToString(),
                "Order placed",
                order.PlacedAtUtc,
                true,
                null)
        };

        timeline.AddRange(order.StatusHistory
            .OrderBy(entry => entry.CreatedAtUtc)
            .Select(entry => new VendorOrderTimelineItemDto(
                entry.NewStatus.ToString(),
                entry.NewStatus.ToString(),
                entry.CreatedAtUtc,
                true,
                entry.Note)));

        return timeline;
    }

    private static bool IsLate(OrderStatus status, DateTime placedAtUtc)
    {
        if (status is OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded)
        {
            return false;
        }

        return DateTime.UtcNow - placedAtUtc > TimeSpan.FromMinutes(45);
    }

    private sealed record TrackingStepDefinition(
        string Id,
        string Title,
        string Time,
        bool IsActive,
        bool IsCompleted);

    private sealed record AdminAddressSnapshot(string AddressLine, string City, string Area, string ContactPhone);

    private sealed record AdminOrderProjection(AdminOrderListItemDto ListItem);

    private async Task<Dictionary<Guid, AdminAddressSnapshot>> LoadAddressMapAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Where(order => orderIds.Contains(order.Id))
            .Join(
                _dbContext.CustomerAddresses.AsNoTracking(),
                order => order.CustomerAddressId,
                address => address.Id,
                (order, address) => new
                {
                    order.Id,
                    Address = new AdminAddressSnapshot(
                        address.AddressLine,
                        address.City ?? string.Empty,
                        address.Area ?? string.Empty,
                        address.ContactPhone ?? string.Empty)
                })
            .ToDictionaryAsync(item => item.Id, item => item.Address, cancellationToken);
    }

    private async Task<Dictionary<Guid, Payment>> LoadPaymentMapAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Payments
            .AsNoTracking()
            .Where(payment => orderIds.Contains(payment.OrderId))
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .GroupBy(payment => payment.OrderId)
            .Select(group => group.First())
            .ToDictionaryAsync(payment => payment.OrderId, payment => payment, cancellationToken);
    }

    private async Task<Dictionary<Guid, List<Refund>>> LoadRefundMapAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Refunds
            .AsNoTracking()
            .Include(refund => refund.Payment)
            .Where(refund => orderIds.Contains(refund.Payment.OrderId))
            .GroupBy(refund => refund.Payment.OrderId)
            .ToDictionaryAsync(group => group.Key, group => group.ToList(), cancellationToken);
    }

    private async Task<Dictionary<Guid, DeliveryAssignment>> LoadAssignmentMapAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        return await _dbContext.DeliveryAssignments
            .AsNoTracking()
            .Include(assignment => assignment.Driver)
            .ThenInclude(driver => driver!.User)
            .Where(assignment => orderIds.Contains(assignment.OrderId))
            .OrderByDescending(assignment => assignment.CreatedAtUtc)
            .GroupBy(assignment => assignment.OrderId)
            .Select(group => group.First())
            .ToDictionaryAsync(item => item.OrderId, item => item, cancellationToken);
    }

    private async Task<IReadOnlyList<AdminDriverCandidateDto>> LoadDriverCandidatesAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var activeZones = await _dbContext.DeliveryZones
            .AsNoTracking()
            .Where(zone => zone.IsActive)
            .ToListAsync(cancellationToken);

        var dispatchContext = DeliveryDispatchScoring.BuildContext(
            activeZones,
            order.VendorBranch?.Latitude,
            order.VendorBranch?.Longitude,
            order.Vendor?.City);

        var drivers = await _dbContext.Drivers
            .AsNoTracking()
            .Include(driver => driver.User)
            .Where(driver =>
                driver.Status == AccountStatus.Active &&
                driver.VerificationStatus == DriverVerificationStatus.Approved)
            .OrderByDescending(driver => driver.IsAvailable)
            .ThenBy(driver => driver.User.FullName)
            .Take(12)
            .ToListAsync(cancellationToken);

        var assignmentCounts = await _dbContext.DeliveryAssignments
            .AsNoTracking()
            .Where(assignment => assignment.DriverId.HasValue)
            .GroupBy(assignment => assignment.DriverId!.Value)
            .Select(group => new { DriverId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.DriverId, item => item.Count, cancellationToken);

        var driverIds = drivers.Select(driver => driver.Id).ToList();

        var latestLocations = await _dbContext.DriverLocations
            .AsNoTracking()
            .Where(location => driverIds.Contains(location.DriverId))
            .GroupBy(location => location.DriverId)
            .Select(group => group.OrderByDescending(location => location.RecordedAtUtc).First())
            .ToDictionaryAsync(location => location.DriverId, cancellationToken);

        var reliabilityData = await _dbContext.DeliveryAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.DriverId.HasValue &&
                driverIds.Contains(assignment.DriverId.Value) &&
                (assignment.Status == AssignmentStatus.Delivered || assignment.Status == AssignmentStatus.Failed))
            .GroupBy(assignment => assignment.DriverId!.Value)
            .Select(group => new
            {
                DriverId = group.Key,
                Completed = group.Count(assignment => assignment.Status == AssignmentStatus.Delivered),
                Failed = group.Count(assignment => assignment.Status == AssignmentStatus.Failed)
            })
            .ToDictionaryAsync(item => item.DriverId, cancellationToken);

        var commitmentSummaries = await _driverCommitmentPolicyService.GetDriverSummariesAsync(driverIds, cancellationToken);

        var utcNow = DateTime.UtcNow;

        return drivers
            .Select((driver, index) =>
            {
                latestLocations.TryGetValue(driver.Id, out var latestLocation);
                assignmentCounts.TryGetValue(driver.Id, out var activeOrders);
                reliabilityData.TryGetValue(driver.Id, out var reliability);

                var totalResolvedAssignments = (reliability?.Completed ?? 0) + (reliability?.Failed ?? 0);
                var reliabilityScore = totalResolvedAssignments > 0
                    ? (decimal)(reliability!.Completed) / totalResolvedAssignments * 100
                    : 50m;

                var evaluation = DeliveryDispatchScoring.EvaluateCandidate(
                    driver,
                    latestLocation,
                    activeOrders,
                    reliabilityScore,
                    commitmentSummaries.TryGetValue(driver.Id, out var commitmentSummary)
                        ? commitmentSummary.CommitmentScore
                        : 100m,
                    dispatchContext,
                    utcNow);

                var lastActivity = latestLocation is null
                    ? "No GPS"
                    : evaluation.GpsFresh
                        ? "Live now"
                        : $"{Math.Max(1, (int)(utcNow - latestLocation.RecordedAtUtc).TotalMinutes)}m ago";

                var candidateStatus = evaluation.MatchReason switch
                {
                    "region-city-live-gps" => "AVAILABLE",
                    "same-region-city" => "REGION_MATCH",
                    "same-city-fallback" => "CITY_FALLBACK",
                    _ => "LOW_PRIORITY"
                };

                return new AdminDriverCandidateDto(
                    driver.Id.ToString(),
                    driver.User.FullName,
                    $"#DRV-{driver.Id.ToString("N")[..6].ToUpperInvariant()}",
                    driver.User.PhoneNumber ?? string.Empty,
                    driver.City ?? dispatchContext.PickupCity ?? "Unknown",
                    driver.Address ?? "Coverage area",
                    candidateStatus,
                    Math.Round(evaluation.DistanceKm, 1),
                    activeOrders,
                    Math.Round(evaluation.ReliabilityScore / 20m, 1),
                    Math.Round(Math.Max(0m, 100m - evaluation.ReliabilityScore), 1),
                    lastActivity,
                    BuildInitials(driver.User.FullName),
                    (index % 3) switch
                    {
                        0 => "from-teal-500 to-cyan-500",
                        1 => "from-amber-500 to-orange-500",
                        _ => "from-rose-500 to-pink-500"
                    },
                    evaluation.ReliabilityScore < 70m ||
                    evaluation.MatchReason == "out-of-area-low-priority" ||
                    evaluation.CommitmentScore < 70m,
                    true,
                    evaluation.MatchReason,
                    evaluation.CommitmentScore,
                    evaluation.CommitmentAdjustmentReason,
                    evaluation.GpsFresh,
                    evaluation.LowConfidenceGps,
                    evaluation.DistanceBucket);
            })
            .OrderBy(candidate => candidate.DistanceKm)
            .ThenBy(candidate => candidate.ActiveOrders)
            .ThenByDescending(candidate => candidate.Rating)
            .ToList();
    }

    private static AdminOrderProjection BuildAdminOrderProjection(
        Order order,
        AdminAddressSnapshot? address,
        Payment? payment,
        IReadOnlyList<Refund>? refunds,
        DeliveryAssignment? assignment)
    {
        var listItem = BuildAdminOrderListItem(order, address, payment, refunds, assignment);
        return new AdminOrderProjection(listItem);
    }

    private AdminOrderDetailDto BuildAdminOrderDetail(
        Order order,
        AdminAddressSnapshot? address,
        Payment? payment,
        IReadOnlyList<Refund>? refunds,
        DeliveryAssignment? assignment,
        IReadOnlyList<AdminDriverCandidateDto> driverCandidates)
    {
        var baseItem = BuildAdminOrderListItem(order, address, payment, refunds, assignment);
        var operationalCase = BuildOperationalCase(order, refunds);
        var placedAtLocal = order.PlacedAtUtc.ToLocalTime();
        var merchantLocation = string.Join(", ", new[] { order.VendorBranch?.AddressLine, order.Vendor?.City, order.Vendor?.NationalAddress }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        var timeline = BuildAdminTimeline(order, payment, assignment, operationalCase);
        var activities = BuildAdminActivities(order, payment, refunds, assignment, operationalCase);
        var candidateScoreBreakdown = BuildCandidateScoreBreakdown(assignment, driverCandidates);

        return new AdminOrderDetailDto(
            order.Id,
            baseItem.DisplayId,
            order.User.FullName,
            address?.ContactPhone ?? order.User.PhoneNumber ?? string.Empty,
            order.User.Email ?? string.Empty,
            BuildCustomerAddress(address),
            order.Vendor?.BusinessNameAr ?? string.Empty,
            order.VendorBranch?.Name ?? "Main branch",
            merchantLocation,
            assignment?.DriverId?.ToString(),
            assignment?.Driver?.User?.FullName ?? string.Empty,
            assignment?.Driver?.User?.PhoneNumber ?? string.Empty,
            assignment?.Driver?.VehicleType?.ToString() ?? "Delivery vehicle",
            assignment?.Driver?.LicenseNumber ?? "N/A",
            address?.City ?? order.Vendor?.City ?? string.Empty,
            address?.Area ?? string.Empty,
            CalculateSlaScore(order, assignment),
            placedAtLocal.ToString("yyyy-MM-dd"),
            placedAtLocal.ToString("hh:mm tt", CultureInfo.InvariantCulture),
            baseItem.Status,
            baseItem.PaymentStatus,
            baseItem.FulfillmentStatus,
            baseItem.DispatchState,
            baseItem.DispatchReason,
            BuildPaymentMethodLabel(order.PaymentMethod),
            BuildExpectedDeliveryWindow(order, assignment),
            payment?.ProviderTransactionId ?? $"ORD-{order.OrderNumber}",
            BuildPaymentStatusNote(order, payment, refunds),
            BuildFulfillmentStatusNote(order, assignment),
            BuildSupportSummary(baseItem.IsLate, operationalCase),
            BuildAlertLabel(baseItem.IsLate, operationalCase, baseItem.Status),
            ResolveLastUpdatedAtUtc(order),
            order.Subtotal,
            order.DeliveryFee,
            Math.Max(0, order.TotalAmount - order.Subtotal - order.DeliveryFee),
            order.TotalAmount,
            order.Items.Select(item => new AdminOrderItemDto(
                item.ProductName,
                "General",
                item.Quantity.ToString(CultureInfo.InvariantCulture),
                item.UnitPrice,
                item.LineTotal,
                "inventory_2",
                item.MasterProductId == Guid.Empty ? item.Id.ToString("N")[..8].ToUpperInvariant() : item.MasterProductId.ToString("N")[..8].ToUpperInvariant()))
                .ToList(),
            timeline,
            activities,
            driverCandidates,
            candidateScoreBreakdown,
            BuildCancellationSummary(order, refunds),
            operationalCase);
    }

    private static AdminOrderListItemDto BuildAdminOrderListItem(
        Order order,
        AdminAddressSnapshot? address,
        Payment? payment,
        IReadOnlyList<Refund>? refunds,
        DeliveryAssignment? assignment)
    {
        var placedAtLocal = order.PlacedAtUtc.ToLocalTime();
        var isLate = IsLate(order.Status, order.PlacedAtUtc);
        var operationalCase = BuildOperationalCase(order, refunds);

        return new AdminOrderListItemDto(
            order.Id,
            $"#{order.OrderNumber}",
            order.User.FullName,
            address?.ContactPhone ?? order.User.PhoneNumber ?? string.Empty,
            order.Vendor.BusinessNameAr,
            order.VendorBranch?.Name ?? "Main branch",
            placedAtLocal.ToString("yyyy-MM-dd"),
            placedAtLocal.ToString("hh:mm tt", CultureInfo.InvariantCulture),
            MapAdminStatus(order.Status),
            MapAdminPaymentStatus(order.PaymentStatus, refunds),
            MapFulfillmentStatus(order.Status, assignment),
            BuildDispatchState(order.Status, assignment),
            BuildDispatchReason(order, assignment),
            BuildPaymentMethodLabel(order.PaymentMethod),
            ResolveLastUpdatedAtUtc(order),
            order.TotalAmount,
            isLate,
            operationalCase is not null || isLate,
            BuildCancellationReason(order),
            operationalCase);
    }

    private static bool MatchesAdminOrderFilters(
        AdminOrderProjection item,
        string? search,
        string? status,
        string? paymentStatus,
        string? fulfillmentStatus,
        string? queueView)
    {
        var list = item.ListItem;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            var matchesSearch =
                list.DisplayId.ToLowerInvariant().Contains(normalizedSearch) ||
                list.CustomerName.ToLowerInvariant().Contains(normalizedSearch) ||
                list.CustomerPhone.ToLowerInvariant().Contains(normalizedSearch) ||
                list.MerchantName.ToLowerInvariant().Contains(normalizedSearch);

            if (!matchesSearch)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(list.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus) && !string.Equals(paymentStatus, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(list.PaymentStatus, paymentStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(fulfillmentStatus) && !string.Equals(fulfillmentStatus, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(list.FulfillmentStatus, fulfillmentStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return queueView?.ToUpperInvariant() switch
        {
            "ACTIVE" => list.Status != "CANCELLED" && list.Status != "COMPLETED",
            "LATE" => list.IsLate,
            "PAYMENT_ISSUES" => list.PaymentStatus is "FAILED" or "PENDING" or "COD_PENDING",
            "REFUNDS" => list.PaymentStatus is "REFUNDED" or "PARTIALLY_REFUNDED" || list.OperationalCase?.Type == "REFUND",
            _ => true
        };
    }

    private static bool MatchesAdminSupportCaseFilters(
        AdminOrderSupportCaseListItemDto item,
        string? search,
        string? type,
        string? status,
        string? priority,
        string? queue)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            var matchesSearch =
                item.Id.ToString().ToLowerInvariant().Contains(normalizedSearch) ||
                item.OrderId.ToString().ToLowerInvariant().Contains(normalizedSearch) ||
                item.CustomerName.ToLowerInvariant().Contains(normalizedSearch) ||
                item.CustomerEmail.ToLowerInvariant().Contains(normalizedSearch) ||
                item.MerchantName.ToLowerInvariant().Contains(normalizedSearch) ||
                item.Type.ToLowerInvariant().Contains(normalizedSearch) ||
                item.Reason.ToLowerInvariant().Contains(normalizedSearch);

            if (!matchesSearch)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(type) &&
            !string.Equals(type, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(priority) &&
            !string.Equals(priority, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Priority, priority, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(queue) &&
            !string.Equals(queue, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Queue, queue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static AdminOrderSupportCaseListItemDto BuildAdminSupportCaseListItem(
        OrderSupportCase supportCase,
        Payment? payment,
        IReadOnlyList<Refund>? refunds)
    {
        var order = supportCase.Order;
        var amount = supportCase.ApprovedRefundAmount
            ?? supportCase.RequestedRefundAmount
            ?? refunds?.OrderByDescending(item => item.CreatedAtUtc).FirstOrDefault()?.Amount
            ?? order.TotalAmount;

        var createdAt = supportCase.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture);
        var sla = supportCase.SlaDueAtUtc.HasValue
            ? supportCase.SlaDueAtUtc.Value.ToLocalTime().ToString("g", CultureInfo.InvariantCulture)
            : "No SLA";

        return new AdminOrderSupportCaseListItemDto(
            supportCase.Id,
            order.Id,
            order.Id.ToString(),
            order.User.FullName,
            order.User.Email ?? string.Empty,
            order.Vendor.BusinessNameAr,
            MapSupportCaseType(supportCase.Type),
            supportCase.ReasonCode ?? supportCase.Message,
            amount,
            MapAdminSupportCaseStatus(supportCase.Status),
            MapSupportCasePriority(supportCase.Priority),
            supportCase.AssignedAdminId.HasValue ? "Assigned admin" : ResolveQueueLabel(supportCase.Queue),
            MapSupportCaseQueue(supportCase.Queue),
            MapRiskLevel(supportCase.Priority),
            createdAt,
            sla,
            supportCase.CustomerVisibleNote ?? supportCase.DecisionNotes ?? supportCase.Message,
            BuildPaymentMask(payment, order),
            BuildCustomerSummary(order, supportCase),
            BuildMerchantSummary(order, supportCase),
            supportCase.Attachments
                .Select(attachment => new OrderSupportCaseAttachmentDto(
                    attachment.FileName,
                    attachment.FileUrl))
                .ToList(),
            supportCase.Activities
                .OrderByDescending(activity => activity.CreatedAtUtc)
                .Take(6)
                .Select(activity => new AdminOrderSupportCaseTimelineItemDto(
                    activity.Title,
                    activity.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture),
                    ResolveTimelineTone(activity.Action, supportCase.Status)))
                .ToList());
    }

    private static string MapAdminSupportCaseStatus(OrderSupportCaseStatus status) =>
        status switch
        {
            OrderSupportCaseStatus.Submitted => "open",
            OrderSupportCaseStatus.InReview => "review",
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "merchant",
            OrderSupportCaseStatus.Approved => "review",
            _ => "resolved"
        };

    private static string MapRiskLevel(OrderSupportCasePriority priority) =>
        priority switch
        {
            OrderSupportCasePriority.Critical => "high",
            OrderSupportCasePriority.High => "high",
            OrderSupportCasePriority.Medium => "medium",
            _ => "low"
        };

    private static string BuildPaymentMask(Payment? payment, Order order)
    {
        if (!string.IsNullOrWhiteSpace(payment?.ProviderTransactionId))
        {
            var suffix = payment.ProviderTransactionId.Length <= 4
                ? payment.ProviderTransactionId
                : payment.ProviderTransactionId[^4..];
            return $"**** {suffix}";
        }

        return order.PaymentMethod.ToString().ToUpperInvariant();
    }

    private static string BuildCustomerSummary(Order order, OrderSupportCase supportCase) =>
        $"Customer {order.User.FullName} opened a {MapSupportCaseType(supportCase.Type).Replace('_', ' ')} case for order {order.OrderNumber}.";

    private static string BuildMerchantSummary(Order order, OrderSupportCase supportCase) =>
        $"Merchant {order.Vendor.BusinessNameAr} is currently routed through the {ResolveQueueLabel(supportCase.Queue)} queue.";

    private static string ResolveTimelineTone(string action, OrderSupportCaseStatus status)
    {
        if (action is "approved" or "resolved")
        {
            return "warning";
        }

        return status == OrderSupportCaseStatus.AwaitingCustomerEvidence ? "muted" : "primary";
    }

    private static string ResolveOperationalCaseType(OrderSupportCase supportCase)
    {
        var reason = supportCase.ReasonCode?.ToLowerInvariant();
        if (reason is "delivery_delay" or "prep_delay")
        {
            return "ISSUE";
        }

        return "DISPUTE";
    }

    private static string ResolveQueueLabel(OrderSupportCaseQueue queue) =>
        queue switch
        {
            OrderSupportCaseQueue.Finance => "Finance",
            OrderSupportCaseQueue.Operations => "Operations",
            _ => "Support"
        };

    private static string MapAdminStatus(OrderStatus status) =>
        status switch
        {
            OrderStatus.PendingPayment or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => "NEW",
            OrderStatus.Accepted => "PENDING",
            OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned => "IN_PROGRESS",
            OrderStatus.PickedUp or OrderStatus.OnTheWay => "OUT_FOR_DELIVERY",
            OrderStatus.Delivered => "DELIVERED",
            OrderStatus.Refunded => "COMPLETED",
            _ => "CANCELLED"
        };

    private static string MapAdminPaymentStatus(PaymentStatus paymentStatus, IReadOnlyList<Refund>? refunds)
    {
        var latestRefund = refunds?
            .OrderByDescending(refund => refund.CreatedAtUtc)
            .FirstOrDefault();

        if (latestRefund is not null)
        {
            return latestRefund.Amount > 0 && paymentStatus == PaymentStatus.Refunded
                ? "REFUNDED"
                : latestRefund.Amount > 0
                    ? "PARTIALLY_REFUNDED"
                    : paymentStatus switch
                    {
                        PaymentStatus.Pending => "PENDING",
                        PaymentStatus.Paid => "PAID",
                        PaymentStatus.Failed => "FAILED",
                        _ => "PENDING"
                    };
        }

        return paymentStatus switch
        {
            PaymentStatus.Paid => "PAID",
            PaymentStatus.Pending or PaymentStatus.Initiated => "PENDING",
            PaymentStatus.Failed => "FAILED",
            PaymentStatus.Refunded => "REFUNDED",
            _ => "COD_PENDING"
        };
    }

    private static string MapFulfillmentStatus(OrderStatus status, DeliveryAssignment? assignment) =>
        status switch
        {
            OrderStatus.PendingPayment or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance or OrderStatus.Accepted => "QUEUED",
            OrderStatus.Preparing => "PREPARING",
            OrderStatus.ReadyForPickup => "READY_FOR_PICKUP",
            OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned => "DRIVER_ASSIGNED",
            OrderStatus.PickedUp => "PICKED_UP",
            OrderStatus.OnTheWay => "ON_ROUTE",
            OrderStatus.Delivered or OrderStatus.Refunded => "DELIVERED",
            OrderStatus.DeliveryFailed => "FAILED",
            _ => assignment?.Status == Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Failed ? "FAILED" : "CANCELLED"
        };

    private static string BuildDispatchState(OrderStatus status, DeliveryAssignment? assignment) =>
        status switch
        {
            OrderStatus.ReadyForPickup => "PENDING",
            OrderStatus.DriverAssignmentInProgress => "SEARCHING",
            OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay =>
                assignment?.DriverId is not null ? "ASSIGNED" : "SEARCHING",
            OrderStatus.Delivered or OrderStatus.Refunded => "COMPLETED",
            OrderStatus.DeliveryFailed => "FAILED",
            OrderStatus.Cancelled or OrderStatus.VendorRejected => "CANCELLED",
            _ => "NOT_REQUIRED"
        };

    private static string BuildDispatchReason(Order order, DeliveryAssignment? assignment)
    {
        if (!string.IsNullOrWhiteSpace(assignment?.FailureReason))
        {
            return assignment.FailureReason!;
        }

        var latestDispatchNote = order.StatusHistory
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault(item =>
                item.NewStatus is OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned)
            ?.Note;

        if (!string.IsNullOrWhiteSpace(latestDispatchNote))
        {
            return latestDispatchNote!;
        }

        return order.Status switch
        {
            OrderStatus.ReadyForPickup => "Order is ready and waiting for dispatch.",
            OrderStatus.DriverAssignmentInProgress => "Dispatch queue is searching for the best available driver.",
            OrderStatus.DriverAssigned => assignment?.Driver?.User is not null
                ? $"Assigned to {assignment.Driver.User.FullName}."
                : "Driver assignment completed.",
            OrderStatus.PickedUp => "Driver picked up the order.",
            OrderStatus.OnTheWay => "Driver is on the way to the customer.",
            OrderStatus.Delivered => "Delivery completed successfully.",
            OrderStatus.DeliveryFailed => "Delivery attempt failed and needs intervention.",
            _ => "Dispatch is not active for the current order state."
        };
    }

    private static IReadOnlyList<string> BuildCandidateScoreBreakdown(
        DeliveryAssignment? assignment,
        IReadOnlyList<AdminDriverCandidateDto> driverCandidates)
    {
        if (assignment?.DriverId is null)
        {
            return [];
        }

        var matchedCandidate = driverCandidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, assignment.DriverId.Value.ToString(), StringComparison.OrdinalIgnoreCase));

        if (matchedCandidate is null)
        {
            return [];
        }

        return
        [
            $"Match reason: {matchedCandidate.DispatchMatchReason}",
            $"Commitment score: {matchedCandidate.CommitmentScore:0.0}",
            $"GPS freshness: {(matchedCandidate.GpsFresh ? "live" : "stale")}",
            $"Distance bucket: {matchedCandidate.DistanceBucket}",
            $"Distance: {matchedCandidate.DistanceKm:0.0} km",
            $"Active orders: {matchedCandidate.ActiveOrders}",
            $"Rating: {matchedCandidate.Rating:0.0}",
            matchedCandidate.CommitmentAdjustmentReason switch
            {
                "commitment-score-boost" => "Commitment effect: commitment-score-boost",
                "rejection-penalty" => "Commitment effect: rejection-penalty",
                _ => "Commitment effect: neutral"
            },
            matchedCandidate.LowConfidenceGps
                ? "GPS confidence: low (>100m)"
                : "GPS confidence: normal",
            matchedCandidate.Verified ? "Verification: approved" : "Verification: pending"
        ];
    }

    private static string BuildPaymentMethodLabel(PaymentMethodType paymentMethod) =>
        paymentMethod switch
        {
            PaymentMethodType.CashOnDelivery => "Cash on delivery",
            _ => paymentMethod.ToString()
        };

    private static DateTime ResolveLastUpdatedAtUtc(Order order)
    {
        var statusUpdatedAt = order.StatusHistory
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => (DateTime?)item.CreatedAtUtc)
            .FirstOrDefault();

        return statusUpdatedAt ?? order.UpdatedAtUtc;
    }

    private static string BuildCustomerAddress(AdminAddressSnapshot? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        return string.Join(", ", new[] { address.AddressLine, address.Area, address.City }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int CalculateSlaScore(Order order, DeliveryAssignment? assignment)
    {
        var elapsed = (assignment?.DeliveredAtUtc ?? DateTime.UtcNow) - order.PlacedAtUtc;
        var score = 100 - (int)Math.Min(65, elapsed.TotalMinutes / 2);
        return Math.Max(35, score);
    }

    private static string BuildExpectedDeliveryWindow(Order order, DeliveryAssignment? assignment)
    {
        var estimated = assignment?.AcceptedAtUtc?.AddMinutes(30)
            ?? assignment?.OfferedAtUtc?.AddMinutes(40)
            ?? order.PlacedAtUtc.AddMinutes(45);

        return estimated.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture);
    }

    private static string BuildPaymentStatusNote(Order order, Payment? payment, IReadOnlyList<Refund>? refunds)
    {
        if (refunds is { Count: > 0 })
        {
            var refund = refunds.OrderByDescending(item => item.CreatedAtUtc).First();
            return $"Refund {refund.Status} for {refund.Amount:0.00} SAR.";
        }

        return order.PaymentStatus switch
        {
            PaymentStatus.Paid => "Payment confirmed with no active failure trace.",
            PaymentStatus.Failed => "Latest payment attempt failed and needs finance review.",
            PaymentStatus.Pending or PaymentStatus.Initiated => "Payment is pending confirmation before fulfillment moves forward.",
            _ => payment?.ProviderName is not null
                ? $"Processed through {payment.ProviderName}."
                : "Payment state is being monitored."
        };
    }

    private static string BuildFulfillmentStatusNote(Order order, DeliveryAssignment? assignment)
    {
        return MapFulfillmentStatus(order.Status, assignment) switch
        {
            "QUEUED" => "Execution has not started yet and the order is still queued.",
            "PREPARING" => "Vendor is actively preparing the order.",
            "READY_FOR_PICKUP" => "Order is ready and waiting for pickup.",
            "DRIVER_ASSIGNED" => "A driver has been assigned and dispatch is in progress.",
            "PICKED_UP" => "Driver picked up the order and is moving toward delivery.",
            "ON_ROUTE" => "Driver is on the way to the customer.",
            "DELIVERED" => "Delivery completed successfully.",
            "FAILED" => "Fulfillment failed and requires intervention.",
            _ => "Order execution stopped after cancellation."
        };
    }

    private static string BuildSupportSummary(bool isLate, AdminOrderOperationalCaseDto? operationalCase)
    {
        if (operationalCase is not null)
        {
            return $"Open {operationalCase.Type.ToLowerInvariant()} case is routed to {operationalCase.QueueLabel}.";
        }

        return isLate
            ? "Order exceeded the expected SLA and should be monitored by operations."
            : "No active support case is currently attached to the order.";
    }

    private static string BuildAlertLabel(bool isLate, AdminOrderOperationalCaseDto? operationalCase, string status)
    {
        if (operationalCase is not null)
        {
            return operationalCase.Title;
        }

        if (isLate)
        {
            return "Order is running behind SLA";
        }

        return status == "CANCELLED"
            ? "Order has been cancelled"
            : "Order flow is healthy";
    }

    private static string? BuildCancellationReason(Order order) =>
        order.Status switch
        {
            OrderStatus.Cancelled => "Cancelled by operations",
            OrderStatus.VendorRejected => "Rejected by merchant",
            OrderStatus.DeliveryFailed => "Delivery failed",
            _ => null
        };

    private static AdminOrderCancellationSummaryDto? BuildCancellationSummary(Order order, IReadOnlyList<Refund>? refunds)
    {
        var reason = BuildCancellationReason(order);
        if (reason is null)
        {
            return null;
        }

        var latestRefund = refunds?
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        var refundType = latestRefund is null
            ? "none"
            : latestRefund.Amount >= order.TotalAmount ? "full" : "partial";

        return new AdminOrderCancellationSummaryDto(
            reason,
            order.Notes ?? "Cancellation was recorded from the admin workflow.",
            refundType,
            latestRefund is null ? "platform" : "merchant",
            (order.CancelledAtUtc ?? ResolveLastUpdatedAtUtc(order)).ToLocalTime().ToString("g", CultureInfo.InvariantCulture),
            "Operations desk",
            "Your order status was updated to cancelled.");
    }

    private static AdminOrderOperationalCaseDto? BuildOperationalCase(Order order, IReadOnlyList<Refund>? refunds)
    {
        var supportCase = order.SupportCases
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        if (supportCase is not null)
        {
            var status = supportCase.Status switch
            {
                OrderSupportCaseStatus.Submitted => "OPEN",
                OrderSupportCaseStatus.InReview => "OPEN",
                OrderSupportCaseStatus.AwaitingCustomerEvidence => "OPEN",
                OrderSupportCaseStatus.Approved => "RESOLVED",
                _ => "CLOSED"
            };

            return new AdminOrderOperationalCaseDto(
                supportCase.Type == OrderSupportCaseType.ReturnRequest ? "REFUND" : ResolveOperationalCaseType(supportCase),
                status,
                supportCase.CustomerVisibleNote ?? supportCase.Message,
                ResolveQueueLabel(supportCase.Queue),
                supportCase.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture),
                supportCase.UpdatedAtUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture));
        }

        var latestRefund = refunds?
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        if (latestRefund is null)
        {
            return null;
        }

        return new AdminOrderOperationalCaseDto(
            "REFUND",
            latestRefund.Status == PaymentStatus.Refunded ? "RESOLVED" : "OPEN",
            latestRefund.Amount >= order.TotalAmount ? "Full refund review" : "Partial refund review",
            "Finance",
            latestRefund.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture),
            latestRefund.UpdatedAtUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture));
    }

    private static IReadOnlyList<AdminOrderTimelineItemDto> BuildAdminTimeline(
        Order order,
        Payment? payment,
        DeliveryAssignment? assignment,
        AdminOrderOperationalCaseDto? operationalCase)
    {
        var steps = new List<AdminOrderTimelineItemDto>
        {
            new(
                "Order created",
                payment?.Status == PaymentStatus.Paid ? "Payment captured" : "Waiting for payment confirmation",
                order.PlacedAtUtc.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture),
                "COMPLETED",
                false),
            new(
                "Vendor handling",
                BuildFulfillmentStatusNote(order, assignment),
                ResolveStepDate(order.StatusHistory.ToList(), OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.ReadyForPickup)?.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture) ?? "--:--",
                order.Status is OrderStatus.PendingPayment or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance ? "PENDING" : "COMPLETED",
                order.Status is OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.ReadyForPickup),
            new(
                "Delivery progress",
                assignment?.Driver is null ? "Awaiting assignment" : $"Driver: {assignment.Driver.User.FullName}",
                (assignment?.AcceptedAtUtc ?? assignment?.OfferedAtUtc)?.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture) ?? "--:--",
                order.Status is OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay ? "IN_PROGRESS" : order.Status is OrderStatus.Delivered or OrderStatus.Refunded ? "COMPLETED" : "PENDING",
                order.Status is OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay),
            new(
                operationalCase is null ? "Case status" : operationalCase.Title,
                operationalCase is null ? "No open operational case" : operationalCase.QueueLabel,
                operationalCase?.LastUpdatedAt ?? ResolveLastUpdatedAtUtc(order).ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture),
                operationalCase is null ? "PENDING" : operationalCase.Status == "OPEN" ? "IN_PROGRESS" : "COMPLETED",
                operationalCase?.Status == "OPEN")
        };

        return steps;
    }

    private static IReadOnlyList<AdminOrderActivityDto> BuildAdminActivities(
        Order order,
        Payment? payment,
        IReadOnlyList<Refund>? refunds,
        DeliveryAssignment? assignment,
        AdminOrderOperationalCaseDto? operationalCase)
    {
        var activities = order.StatusHistory
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(5)
            .Select(item => new AdminOrderActivityDto(
                $"Order moved to {item.NewStatus}",
                item.ChangedByUserId.HasValue ? "Workflow user" : "System",
                item.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture),
                "status"))
            .ToList();

        if (payment is not null)
        {
            activities.Insert(0, new AdminOrderActivityDto(
                $"Payment state: {payment.Status}",
                payment.ProviderName ?? "Payment gateway",
                (payment.PaidAtUtc ?? payment.FailedAtUtc ?? payment.CreatedAtUtc).ToLocalTime().ToString("g", CultureInfo.InvariantCulture),
                "payment"));
        }

        if (assignment?.Driver is not null)
        {
            activities.Insert(0, new AdminOrderActivityDto(
                $"Driver assigned: {assignment.Driver.User.FullName}",
                "Dispatch",
                assignment.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture),
                "status"));
        }

        if (operationalCase is not null)
        {
            activities.Insert(0, new AdminOrderActivityDto(
                operationalCase.Title,
                operationalCase.QueueLabel,
                operationalCase.LastUpdatedAt,
                "issue"));
        }

        if (refunds is { Count: > 0 })
        {
            var refund = refunds.OrderByDescending(item => item.CreatedAtUtc).First();
            activities.Insert(0, new AdminOrderActivityDto(
                $"Refund {refund.Status}",
                "Finance",
                refund.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture),
                "payment"));
        }

        return activities.Take(8).ToList();
    }

    private static string BuildInitials(string fullName)
    {
        var parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]));

        return string.Concat(parts);
    }

    private static bool IsActiveDeliveryStatus(OrderStatus status)
    {
        return status is OrderStatus.DriverAssigned
            or OrderStatus.PickedUp
            or OrderStatus.OnTheWay;
    }

    private enum TrackingStage
    {
        OrderPlaced,
        VendorConfirmed,
        Preparing,
        OutForDelivery
    }
}
