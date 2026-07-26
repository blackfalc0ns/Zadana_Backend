using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Geography;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Infrastructure.Modules.Orders.Services;

public class OrderReadService : IOrderReadService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDriverCommitmentPolicyService _driverCommitmentPolicyService;
    private readonly IAppCache? _cache;

    private static readonly AppCacheEntryOptions AdminKpiCacheOptions = new(
        Expiration: TimeSpan.FromSeconds(30),
        LocalExpiration: TimeSpan.FromSeconds(15));

    private const string AdminOrdersKpiCacheTag = "orders:admin:kpi";

    public OrderReadService(
        ApplicationDbContext dbContext,
        IDriverCommitmentPolicyService driverCommitmentPolicyService,
        IAppCache? cache = null)
    {
        _dbContext = dbContext;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
        _cache = cache;
    }

    /// <summary>Returns Arabic or English text based on the current request culture.</summary>
    private static string L(string ar, string en) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? ar : en;

    private static string LocalizeCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return string.Empty;
        var clean = city.Trim().ToUpperInvariant();
        return clean switch
        {
            "RIYADH" => L("الرياض", "Riyadh"),
            "JEDDAH" => L("جدة", "Jeddah"),
            "DAMMAM" => L("الدمام", "Dammam"),
            "MAKKAH" => L("مكة", "Makkah"),
            "MADINAH" => L("المدينة", "Madinah"),
            "TAIF" => L("الطائف", "Taif"),
            "TABUK" => L("تبوك", "Tabuk"),
            "ABHA" => L("أبها", "Abha"),
            "KHOBAR" => L("الخبر", "Khobar"),
            "QATIF" => L("القطيف", "Qatif"),
            _ => city
        };
    }

    /// <summary>
    /// Escapes characters that have meaning inside a SQL LIKE pattern so a
    /// user-supplied search term cannot accidentally widen the match. Used
    /// together with <see cref="EF.Functions.Like(DbFunctions, string, string)"/>
    /// to keep search queries seek-friendly without LOWER() hacks.
    /// </summary>
    private static string EscapeLike(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
    }

    public OrderReadService(ApplicationDbContext dbContext)
        : this(dbContext, NoOpDriverCommitmentPolicyService.Instance)
    {
    }

    private sealed class NoOpDriverCommitmentPolicyService : IDriverCommitmentPolicyService
    {
        public static NoOpDriverCommitmentPolicyService Instance { get; } = new();

        public Task<DriverCommitmentSummaryDto> GetDriverSummaryAsync(Guid driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new DriverCommitmentSummaryDto(
                    0,
                    0,
                    0,
                    0,
                    0,
                    100m,
                    DriverCommitmentEnforcementLevel.Healthy.ToString(),
                    true,
                    null,
                    null));

        public Task<IReadOnlyDictionary<Guid, DriverCommitmentSummaryDto>> GetDriverSummariesAsync(
            IReadOnlyCollection<Guid> driverIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, DriverCommitmentSummaryDto> result = new Dictionary<Guid, DriverCommitmentSummaryDto>();
            return Task.FromResult(result);
        }

        public Task ApplyOperationalEnforcementAsync(
            IReadOnlyCollection<Guid> driverIds,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    public async Task<OrderDto?> GetByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.MasterProduct)
                    .ThenInclude(product => product!.Images)
            .FirstOrDefaultAsync(item => item.Id == orderId && item.UserId == userId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        return new OrderDto(
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
                item.MasterProduct?.NameAr ?? item.ProductName,
                item.MasterProduct?.NameEn ?? item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.LineTotal,
                BuildProductImageUrl(item),
                BuildVariantDisplaySize(item),
                BuildPackageTypeName(item),
                item.MasterProduct?.MeasurementValue,
                BuildMeasurementUnitName(item))).ToList());
    }

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
        var orders = await IncludeCustomerOrderItems(query)
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
        var order = await IncludeCustomerOrderItems(_dbContext.Orders.AsNoTracking().AsSplitQuery())
            .Include(order => order.SupportCases)
            .Where(order => order.Id == orderId && order.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        var pickupBranch = await BuildPickupBranchAsync(order, cancellationToken);
        return MapDetail(order, pickupBranch);
    }

    public async Task<CustomerOrderTrackingDto?> GetCustomerOrderTrackingAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .AsSplitQuery()
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

        var isPickup = order.Fulfillment == FulfillmentType.Pickup;
        var timeline = BuildTimeline(order);
        var estimatedDelivery = isPickup
            ? null
            : await BuildEstimatedDeliveryAsync(order, assignment, cancellationToken);
        var driver = isPickup ? null : BuildDriver(assignment);
        var assignedDriver = isPickup ? null : BuildAssignedDriverSummary(assignment);
        var arrivalState = isPickup ? "none" : ResolveArrivalState(assignment);
        var arrivalUpdatedAtUtc = isPickup ? null : ResolveArrivalUpdatedAtUtc(assignment);
        var showDeliveryOtp = !isPickup &&
            (order.Status == OrderStatus.PickedUp || order.Status == OrderStatus.OnTheWay) &&
            assignment is not null &&
            !assignment.DeliveryOtpVerifiedAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(assignment.DeliveryOtpCode);
        var pickupBranch = await BuildPickupBranchAsync(order, cancellationToken);
        var showCustomerPickupOtp = isPickup &&
            order.Status == OrderStatus.ReadyForPickup &&
            !order.PickupOtpVerifiedAtUtc.HasValue;

        return new CustomerOrderTrackingDto(
            new CustomerOrderTrackingOrderDto(order.Id, order.OrderNumber, MapTrackingStatus(order)),
            estimatedDelivery,
            driver,
            assignedDriver,
            BuildDeliveryBreakdown(order),
            MapFulfillmentType(order.Fulfillment),
            arrivalState,
            arrivalUpdatedAtUtc,
            showDeliveryOtp ? assignment!.DeliveryOtpCode : null,
            showDeliveryOtp,
            showCustomerPickupOtp ? order.PickupOtpCode : null,
            showCustomerPickupOtp ? order.PickupOtpExpiresAtUtc : null,
            isPickup ? order.PickupNoShowDeadlineUtc : null,
            pickupBranch,
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
            .Where(item => item.OrderId == orderId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var couponSupportMap = await LoadCouponSupportMapAsync(
            items.Where(item => item.CompensationCouponId.HasValue)
                .Select(item => item.CompensationCouponId!.Value)
                .Distinct()
                .ToList(),
            cancellationToken);

        return items.Select(item => MapSupportCase(item, couponSupportMap)).ToList();
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
            .Where(item => item.OrderId == orderId && item.Id == caseId)
            .FirstOrDefaultAsync(cancellationToken);

        if (supportCase is null)
        {
            return null;
        }

        var couponSupportMap = await LoadCouponSupportMapAsync(
            supportCase.CompensationCouponId.HasValue ? [supportCase.CompensationCouponId.Value] : [],
            cancellationToken);

        return MapSupportCase(supportCase, couponSupportMap);
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
            // Avoid LOWER() on indexed columns — SQL Server's default
            // case-insensitive collation makes LIKE seek-friendly already.
            // OrderNumber is a unique index, so prefer StartsWith to leverage it.
            var s = search.Trim();
            var like = $"%{EscapeLike(s)}%";
            query = query.Where(order =>
                EF.Functions.Like(order.OrderNumber, $"{EscapeLike(s)}%") ||
                EF.Functions.Like(order.User.FullName, like) ||
                (order.User.PhoneNumber != null && EF.Functions.Like(order.User.PhoneNumber, like)));
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
        Guid? branchId,
        string? search,
        string? status,
        string? paymentMethod,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.VendorId == vendorId &&
                order.Status != OrderStatus.PendingPayment &&
                ((order.PaymentMethod != PaymentMethodType.Card &&
                  order.PaymentMethod != PaymentMethodType.BankTransfer) ||
                 order.PaymentStatus == PaymentStatus.Paid ||
                 order.PaymentStatus == PaymentStatus.Refunded ||
                 order.PaymentStatus == PaymentStatus.PartiallyRefunded));

        if (branchId.HasValue)
        {
            query = query.Where(order => order.VendorBranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var like = $"%{EscapeLike(s)}%";
            query = query.Where(order =>
                EF.Functions.Like(order.OrderNumber, $"{EscapeLike(s)}%") ||
                EF.Functions.Like(order.User.FullName, like) ||
                (order.User.PhoneNumber != null && EF.Functions.Like(order.User.PhoneNumber, like)));
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
            order.Fulfillment == FulfillmentType.Pickup ? "pickup" : "delivery",
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
        Guid? branchId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.User)
            .Include(item => item.Items)
                .ThenInclude(item => item.MasterProduct)
                    .ThenInclude(product => product!.Images)
            .Include(item => item.StatusHistory)
            .Include(item => item.Vendor)
            .Where(item =>
                item.VendorId == vendorId &&
                item.Id == orderId &&
                (!branchId.HasValue || item.VendorBranchId == branchId.Value) &&
                item.Status != OrderStatus.PendingPayment &&
                ((item.PaymentMethod != PaymentMethodType.Card &&
                  item.PaymentMethod != PaymentMethodType.BankTransfer) ||
                 item.PaymentStatus == PaymentStatus.Paid ||
                 item.PaymentStatus == PaymentStatus.Refunded ||
                 item.PaymentStatus == PaymentStatus.PartiallyRefunded))
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        var productImageLookup = await LoadMasterProductPrimaryImageLookupAsync(
            order.Items.Select(item => item.MasterProductId),
            cancellationToken);

        var pendingCancellation = await _dbContext.OrderCancellationRequests
            .AsNoTracking()
            .Where(item =>
                item.OrderId == order.Id &&
                item.Status == OrderCancellationRequestStatus.Pending)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new PendingCancellationRequestDto(
                item.Id,
                item.Status.ToString(),
                item.CustomerReason,
                item.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        var customerAddressOptions = await _dbContext.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.UserId == order.UserId)
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.UpdatedAtUtc)
            .Select(address => new CustomerAddressOptionDto(
                address.Id,
                address.Label.HasValue ? address.Label.Value.ToString() : "Address",
                ((address.AddressLine ?? string.Empty) +
                 (string.IsNullOrWhiteSpace(address.Area) ? string.Empty : $", {address.Area}") +
                 (string.IsNullOrWhiteSpace(address.City) ? string.Empty : $", {address.City}")).Trim(',', ' ')))
            .ToListAsync(cancellationToken);

        var customerAddress = order.CustomerAddressId is null
            ? null
            : await _dbContext.CustomerAddresses
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
            : string.Join(", ", new[] { customerAddress.AddressLine, customerAddress.Area, LocalizeCity(customerAddress.City) }
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

        // Live Tracking: vendor, customer, and driver locations
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

        // Fallback: if no branch on the order, use the vendor's first active branch
        if (vendorLocation is null)
        {
            var fallbackBranch = await _dbContext.Set<VendorBranch>()
                .AsNoTracking()
                .Where(b => b.VendorId == order.VendorId && b.IsActive)
                .OrderBy(b => b.CreatedAtUtc)
                .Select(b => new { b.Latitude, b.Longitude })
                .FirstOrDefaultAsync(cancellationToken);
            if (fallbackBranch is not null)
                vendorLocation = new GeoPointDto(fallbackBranch.Latitude, fallbackBranch.Longitude);
        }

        GeoPointDto? customerLocation = null;
        if (customerAddress is { Latitude: not null, Longitude: not null })
        {
            customerLocation = new GeoPointDto(customerAddress.Latitude.Value, customerAddress.Longitude.Value);
        }

        DriverLiveLocationDto? driverLiveLocation = null;
        if (assignment?.DriverId != null && order.Status == OrderStatus.DriverAssigned)
        {
            // Hot path served by the DriverLatestLocations table — primary
            // key seek on DriverId, no scan / sort over the audit history.
            var latestLocation = await _dbContext.DriverLatestLocations
                .AsNoTracking()
                .Where(l => l.DriverId == assignment.DriverId.Value)
                .Select(l => new DriverLiveLocationDto(l.Latitude, l.Longitude, l.AccuracyMeters, l.RecordedAtUtc))
                .FirstOrDefaultAsync(cancellationToken);
            if (latestLocation is not null)
            {
                driverLiveLocation = latestLocation;
            }
        }

        var pickupBranch = await BuildPickupBranchAsync(order, cancellationToken);
        var customerPickupOtpStatus = order.Fulfillment != FulfillmentType.Pickup
            ? "not_applicable"
            : order.PickupOtpVerifiedAtUtc.HasValue
                ? "verified"
                : order.Status == OrderStatus.ReadyForPickup
                    ? "pending"
                    : "not_available";

        return new VendorOrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.User.FullName,
            customerAddress?.ContactPhone ?? order.User.PhoneNumber ?? string.Empty,
            customerAddressText,
            order.UserId,
            order.Status.ToString(),
            order.PaymentStatus.ToString(),
            order.PaymentMethod.ToString(),
            MapFulfillmentType(order.Fulfillment),
            order.Subtotal,
            order.DeliveryFee,
            BuildDeliveryBreakdown(order),
            order.TotalAmount,
            order.Notes,
            order.PlacedAtUtc,
            assignedDriver,
            arrivalState,
            arrivalUpdatedAtUtc,
            null,
            canConfirmPickup,
            order.Fulfillment == FulfillmentType.Pickup ? customerPickupOtpStatus : pickupOtpStatus,
            order.PickupOtpFailedAttempts,
            order.PickupOtpLockedUntilUtc,
            order.PickupNoShowDeadlineUtc,
            pickupBranch,
            pendingCancellation,
            customerAddressOptions,
            vendorLocation,
            customerLocation,
            driverLiveLocation,
            order.Items.Select(item => new OrderItemDto(
                item.Id,
                item.VendorProductId,
                item.MasterProductId,
                item.ProductName,
                item.MasterProduct?.NameAr ?? item.ProductName,
                item.MasterProduct?.NameEn ?? item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.LineTotal,
                BuildProductImageUrl(item, productImageLookup),
                BuildVariantDisplaySize(item),
                BuildPackageTypeName(item),
                item.MasterProduct?.MeasurementValue,
                BuildMeasurementUnitName(item))).ToList(),
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

        // 1. Build IQueryable with server-side filters
        var query = _dbContext.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(order => order.User)
            .Include(order => order.Vendor)
            .Include(order => order.VendorBranch)
            .Include(order => order.SupportCases)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var like = $"%{EscapeLike(s)}%";
            query = query.Where(order =>
                EF.Functions.Like(order.OrderNumber, $"{EscapeLike(s)}%") ||
                EF.Functions.Like(order.User.FullName, like) ||
                (order.User.PhoneNumber != null && EF.Functions.Like(order.User.PhoneNumber, like)) ||
                EF.Functions.Like(order.Vendor.BusinessNameAr, like) ||
                EF.Functions.Like(order.Vendor.BusinessNameEn, like));
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

        // 2. Count + KPI summary. The KPI scans the full Orders table, which
        // is expensive — cache it for 30s. Filter counts (totalCount) stay
        // live since they reflect the user's current filter selection.
        var totalCount = await query.CountAsync(cancellationToken);

        var summary = await GetAdminOrdersKpiSummaryAsync(cancellationToken);

        // 3. Paginate in SQL
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

        // 4. Load related data only for the page
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

    /// <summary>
    /// Returns the admin orders KPI summary. The query scans the full Orders
    /// table, so it's cached for 30s in the distributed cache (HybridCache
    /// uses an in-memory tier first, falling back to Redis). Cache miss runs
    /// the same aggregate query that lived inline before.
    /// </summary>
    private async Task<AdminOrdersSummaryDto> GetAdminOrdersKpiSummaryAsync(CancellationToken cancellationToken)
    {
        if (_cache is null)
        {
            return await ComputeAdminOrdersKpiAsync(cancellationToken);
        }

        return await _cache.GetOrCreateAsync(
            AppCacheKeys.Build("orders", "admin", "kpi", "v1"),
            ComputeAdminOrdersKpiAsync,
            AdminKpiCacheOptions,
            tags: new[] { AdminOrdersKpiCacheTag },
            cancellationToken: cancellationToken);
    }

    private async Task<AdminOrdersSummaryDto> ComputeAdminOrdersKpiAsync(CancellationToken cancellationToken)
    {
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

        return new AdminOrdersSummaryDto(kpi?.Total ?? 0, kpi?.Active ?? 0, kpi?.Late ?? 0, kpi?.PayIssues ?? 0, kpi?.Refunds ?? 0);
    }

    public async Task<AdminOrderDetailDto?> GetAdminOrderDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.User)
            .Include(item => item.Items)
                .ThenInclude(item => item.MasterProduct)
                    .ThenInclude(product => product!.Images)
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

        // Load driver live location if driver is assigned
        var assignment = assignments.GetValueOrDefault(order.Id);
        DriverLiveLocationDto? driverLiveLocation = null;
        if (assignment?.DriverId is not null)
        {
            // PK seek on DriverLatestLocations replaces a top-1 indexed scan
            // over the full DriverLocations history.
            var latestLocation = await _dbContext.DriverLatestLocations
                .AsNoTracking()
                .Where(loc => loc.DriverId == assignment.DriverId.Value)
                .Select(loc => new DriverLiveLocationDto(loc.Latitude, loc.Longitude, loc.AccuracyMeters, loc.RecordedAtUtc))
                .FirstOrDefaultAsync(cancellationToken);
            driverLiveLocation = latestLocation;
        }

        var pickupBranch = await BuildPickupBranchAsync(order, cancellationToken);
        var customerAddresses = await _dbContext.CustomerAddresses
            .AsNoTracking()
            .Where(item => item.UserId == order.UserId)
            .OrderByDescending(item => item.IsDefault)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .Select(item => new CustomerAddressOptionDto(
                item.Id,
                item.Label.HasValue ? item.Label.Value.ToString() : "Address",
                ((item.AddressLine ?? string.Empty) +
                 (string.IsNullOrWhiteSpace(item.Area) ? string.Empty : $", {item.Area}") +
                 (string.IsNullOrWhiteSpace(item.City) ? string.Empty : $", {item.City}")).Trim(',', ' ')))
            .ToListAsync(cancellationToken);

        return BuildAdminOrderDetail(
            order,
            address.GetValueOrDefault(order.Id),
            payment.GetValueOrDefault(order.Id),
            refunds.GetValueOrDefault(order.Id),
            assignment,
            driverCandidates,
            driverLiveLocation,
            pickupBranch,
            customerAddresses);
    }

    public async Task<AdminOrderSupportCasesListDto> GetAdminOrderSupportCasesAsync(
        string? search,
        string? type,
        string? status,
        string? priority,
        string? queue,
        string? initiatorRole,
        Guid? vendorId,
        Guid? driverId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 20 : pageSize;

        var casesQuery = _dbContext.OrderSupportCases
            .AsNoTracking()
            .AsQueryable();

        if (vendorId.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.Order != null && item.Order.VendorId == vendorId.Value);
        }

        if (driverId.HasValue)
        {
            casesQuery = casesQuery.Where(item =>
                item.DriverId == driverId.Value ||
                (item.OrderId.HasValue &&
                 _dbContext.DeliveryAssignments.Any(assignment => assignment.OrderId == item.OrderId.Value && assignment.DriverId == driverId.Value)));
        }

        casesQuery = ApplyAdminSupportCaseFilters(casesQuery, search, type, status, priority, queue, initiatorRole);

        var totalCount = await casesQuery.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 1
            : Math.Max(1, (int)Math.Ceiling(totalCount / (double)normalizedPageSize));
        var safePage = Math.Min(normalizedPage, totalPages);

        var caseIds = await casesQuery
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.Id)
            .Skip((safePage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        if (caseIds.Count == 0)
        {
            return new AdminOrderSupportCasesListDto(
                [],
                safePage,
                normalizedPageSize,
                totalCount,
                totalPages,
                safePage > 1,
                safePage < totalPages);
        }

        var cases = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Where(item => caseIds.Contains(item.Id))
            .Include(item => item.Order)
                .ThenInclude(order => order.User)
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .Include(item => item.Attachments)
            .Include(item => item.Activities)
            .ToListAsync(cancellationToken);

        var orderedCases = caseIds
            .Join(cases, id => id, supportCase => supportCase.Id, (_, supportCase) => supportCase)
            .ToList();

        var orderIds = orderedCases
            .Where(item => item.OrderId.HasValue)
            .Select(item => item.OrderId!.Value)
            .Distinct()
            .ToList();
        var paymentMap = await LoadPaymentMapAsync(orderIds, cancellationToken);
        var refundMap = await LoadRefundMapAsync(orderIds, cancellationToken);
        var recoveryMap = await LoadVendorRecoveryMapAsync(caseIds, cancellationToken);
        var couponSupportMap = await LoadCouponSupportMapAsync(
            orderedCases.Where(item => item.CompensationCouponId.HasValue)
                .Select(item => item.CompensationCouponId!.Value)
                .Distinct()
                .ToList(),
            cancellationToken);

        var paged = orderedCases
            .Select(item => BuildAdminSupportCaseListItem(
                item,
                item.OrderId.HasValue ? paymentMap.GetValueOrDefault(item.OrderId.Value) : null,
                item.OrderId.HasValue ? refundMap.GetValueOrDefault(item.OrderId.Value) : null,
                recoveryMap.GetValueOrDefault(item.Id),
                couponSupportMap))
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

    private static IQueryable<OrderSupportCase> ApplyAdminSupportCaseTypeFilter(
        IQueryable<OrderSupportCase> query,
        string? type)
    {
        if (string.IsNullOrWhiteSpace(type) ||
            string.Equals(type, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(item => item.Type != OrderSupportCaseType.Complaint);
        }

        if (type.Contains(',', StringComparison.Ordinal))
        {
            var tokens = type
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => token.ToLowerInvariant())
                .ToHashSet(StringComparer.Ordinal);

            return query.Where(item =>
                (tokens.Contains("return_request") && item.Type == OrderSupportCaseType.ReturnRequest) ||
                (tokens.Contains("driver_dispute") && item.Type == OrderSupportCaseType.DriverDispute) ||
                (tokens.Contains("driver_report") && item.Type == OrderSupportCaseType.DriverReport) ||
                ((tokens.Contains("driver_account") || tokens.Contains("driver_account_appeal")) &&
                 item.Type == OrderSupportCaseType.DriverAccountAppeal) ||
                ((tokens.Contains("complaint") || tokens.Contains("support")) &&
                 item.Type == OrderSupportCaseType.Complaint));
        }

        var normalizedType = type.Trim().ToLowerInvariant();
        return normalizedType switch
        {
            "return_request" => query.Where(item => item.Type == OrderSupportCaseType.ReturnRequest),
            "complaint" or "support" => query.Where(item => item.Type == OrderSupportCaseType.Complaint),
            "driver_report" => query.Where(item => item.Type == OrderSupportCaseType.DriverReport),
            "driver_dispute" => query.Where(item => item.Type == OrderSupportCaseType.DriverDispute),
            "driver_account" or "driver_account_appeal" => query.Where(item => item.Type == OrderSupportCaseType.DriverAccountAppeal),
            _ => query
        };
    }

    private static IQueryable<OrderSupportCase> ApplyAdminSupportCaseFilters(
        IQueryable<OrderSupportCase> query,
        string? search,
        string? type,
        string? status,
        string? priority,
        string? queue,
        string? initiatorRole)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var pattern = $"%{normalizedSearch}%";

            if (Guid.TryParse(normalizedSearch, out var parsedId))
            {
                query = query.Where(item => item.Id == parsedId || item.OrderId == parsedId || item.DriverId == parsedId);
            }
            else
            {
                query = query.Where(item =>
                    (item.Order != null && EF.Functions.Like(item.Order.User.FullName, pattern)) ||
                    (item.Order != null && item.Order.User.Email != null && EF.Functions.Like(item.Order.User.Email, pattern)) ||
                    (item.Order != null && EF.Functions.Like(item.Order.Vendor.BusinessNameAr, pattern)) ||
                    (item.Order != null && item.Order.Vendor.BusinessNameEn != null && EF.Functions.Like(item.Order.Vendor.BusinessNameEn, pattern)) ||
                    (item.ReasonCode != null && EF.Functions.Like(item.ReasonCode, pattern)) ||
                    EF.Functions.Like(item.Message, pattern));
            }
        }

        query = ApplyAdminSupportCaseTypeFilter(query, type);

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = normalizedStatus switch
            {
                "active" => query.Where(item =>
                    item.Status != OrderSupportCaseStatus.Resolved &&
                    item.Status != OrderSupportCaseStatus.Rejected),
                "review" or "in_review" => query.Where(item => item.Status == OrderSupportCaseStatus.InReview),
                "merchant" or "awaiting_customer_evidence" => query.Where(item => item.Status == OrderSupportCaseStatus.AwaitingCustomerEvidence),
                "open" or "submitted" => query.Where(item => item.Status == OrderSupportCaseStatus.Submitted),
                "approved" => query.Where(item => item.Status == OrderSupportCaseStatus.Approved),
                "rejected" => query.Where(item => item.Status == OrderSupportCaseStatus.Rejected),
                "resolved" => query.Where(item => item.Status == OrderSupportCaseStatus.Resolved),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(priority) &&
            !string.Equals(priority, "ALL", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<OrderSupportCasePriority>(priority.Trim(), true, out var parsedPriority))
        {
            query = query.Where(item => item.Priority == parsedPriority);
        }

        if (!string.IsNullOrWhiteSpace(queue) &&
            !string.Equals(queue, "ALL", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<OrderSupportCaseQueue>(queue.Trim(), true, out var parsedQueue))
        {
            query = query.Where(item => item.Queue == parsedQueue);
        }

        if (!string.IsNullOrWhiteSpace(initiatorRole) &&
            !string.Equals(initiatorRole, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedRole = initiatorRole.Trim().ToLowerInvariant();
            query = query.Where(item =>
                item.InitiatorRole != null &&
                item.InitiatorRole.ToLower() == normalizedRole);
        }

        return query;
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

        var payment = supportCase.OrderId.HasValue
            ? await _dbContext.Payments
                .AsNoTracking()
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefaultAsync(item => item.OrderId == supportCase.OrderId.Value, cancellationToken)
            : null;

        var refunds = supportCase.OrderId.HasValue
            ? await _dbContext.Refunds
                .AsNoTracking()
                .Where(item => item.Payment.OrderId == supportCase.OrderId.Value)
                .ToListAsync(cancellationToken)
            : new List<Refund>();

        var recovery = await _dbContext.VendorRecoveries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.OrderSupportCaseId == supportCase.Id, cancellationToken);

        var couponSupportMap = await LoadCouponSupportMapAsync(
            supportCase.CompensationCouponId.HasValue ? [supportCase.CompensationCouponId.Value] : [],
            cancellationToken);

        return BuildAdminSupportCaseListItem(supportCase, payment, refunds, recovery, couponSupportMap);
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
                    ResolveCustomerItemName(item),
                    item.Quantity,
                    item.UnitPrice,
                    BuildProductImageUrl(item),
                    BuildVariantDisplaySize(item),
                    BuildPackageTypeName(item),
                    item.MasterProduct?.MeasurementValue,
                    BuildMeasurementUnitName(item)))
                .ToList());

    private async Task<PickupBranchDto?> BuildPickupBranchAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Fulfillment != FulfillmentType.Pickup || !order.VendorBranchId.HasValue)
        {
            return null;
        }

        var branch = await _dbContext.VendorBranches
            .AsNoTracking()
            .Include(item => item.OperatingHours)
            .FirstOrDefaultAsync(item => item.Id == order.VendorBranchId.Value, cancellationToken);

        if (branch is null)
        {
            return null;
        }

        var address = SaudiGeographyDisplay.FormatBranchAddress(
            branch.AddressLine,
            branch.City,
            branch.Region);

        return new PickupBranchDto(
            branch.Name,
            address,
            BranchOperatingHoursSupport.BuildHoursTodayLabel(branch.OperatingHours.ToList(), DateTime.UtcNow));
    }

    private static CustomerOrderDetailDto MapDetail(Order order, PickupBranchDto? pickupBranch)
    {
        var showCustomerPickupOtp = order.Fulfillment == FulfillmentType.Pickup &&
            order.Status == OrderStatus.ReadyForPickup &&
            !order.PickupOtpVerifiedAtUtc.HasValue;

        return new CustomerOrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.PlacedAtUtc,
            order.TotalAmount,
            MapCustomerFacingStatus(order),
            MapCustomerPaymentStatus(order.PaymentStatus),
            MapCustomerPaymentMethod(order.PaymentMethod),
            MapFulfillmentType(order.Fulfillment),
            CanRetryPayment(order),
            CanDelete(order),
            CanCancel(order.Status),
            order.Items.Count,
            new CustomerOrderPriceSummaryDto(
                order.Subtotal,
                order.DeliveryFee,
                order.TotalAmount),
            BuildDeliveryBreakdown(order),
            showCustomerPickupOtp ? order.PickupOtpCode : null,
            showCustomerPickupOtp ? order.PickupOtpExpiresAtUtc : null,
            order.PickupNoShowDeadlineUtc,
            pickupBranch,
            order.Items
                .Select(item => new CustomerOrderProductDto(
                    item.Id,
                    ResolveCustomerItemName(item),
                    item.Quantity,
                    item.UnitPrice,
                    BuildProductImageUrl(item),
                    BuildVariantDisplaySize(item),
                    BuildPackageTypeName(item),
                    item.MasterProduct?.MeasurementValue,
                    BuildMeasurementUnitName(item)))
                .ToList(),
            ResolveActiveSupportCaseSummary(order.SupportCases));
    }

    private static OrderDeliveryBreakdownDto BuildDeliveryBreakdown(Order order) =>
        new(
            order.DriverToVendorDistanceKm,
            order.VendorToCustomerDistanceKm,
            order.DriverToVendorFee,
            order.VendorToCustomerFee,
            order.DeliveryFee,
            order.DriverToVendorPricingSource ?? "fallback",
            order.VendorToCustomerPricingSource ?? "fallback",
            order.DeliveryPricingMode ?? "estimated",
            order.UsedEstimatedDriverPricing,
            order.DeliveryQuoteStatus ?? "estimated_locked",
            order.PricingOriginType,
            order.PricingOriginDriverId,
            order.DeliveryQuoteLockedAtUtc,
            order.DeliveryQuoteVersion,
            order.HasDeliveryAnomalyWarning,
            order.ActualAssignedDriverPickupDistanceKm,
            order.ActualDispatchDeviationPercent);

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildProductImageUrl(
        OrderItem item,
        IReadOnlyDictionary<Guid, string?>? productImageLookup = null)
    {
        // Prefer the historical snapshot captured at order time
        if (!string.IsNullOrWhiteSpace(item.SnapshotImageUrl))
        {
            return item.SnapshotImageUrl;
        }

        // Fallback to current MasterProduct images (legacy orders without snapshot)
        var navigationImage = item.MasterProduct?.Images
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.DisplayOrder)
            .Select(image => image.Url)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(navigationImage))
        {
            return navigationImage;
        }

        if (productImageLookup is not null &&
            productImageLookup.TryGetValue(item.MasterProductId, out var lookupImage) &&
            !string.IsNullOrWhiteSpace(lookupImage))
        {
            return lookupImage;
        }

        return null;
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> LoadMasterProductPrimaryImageLookupAsync(
        IEnumerable<Guid> masterProductIds,
        CancellationToken cancellationToken)
    {
        var ids = masterProductIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        var rows = await _dbContext.Set<MasterProductImage>()
            .AsNoTracking()
            .Where(image => ids.Contains(image.MasterProductId))
            .Select(image => new
            {
                image.MasterProductId,
                image.Url,
                image.IsPrimary,
                image.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.MasterProductId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.IsPrimary)
                    .ThenBy(row => row.DisplayOrder)
                    .Select(row => row.Url)
                    .FirstOrDefault());
    }

    private static IQueryable<Order> IncludeCustomerOrderItems(IQueryable<Order> query) =>
        query
            .Include(order => order.Items)
                .ThenInclude(item => item.MasterProduct)
                    .ThenInclude(product => product!.Images)
            .Include(order => order.Items)
                .ThenInclude(item => item.MasterProduct)
                    .ThenInclude(product => product!.PackageType)
            .Include(order => order.Items)
                .ThenInclude(item => item.MasterProduct)
                    .ThenInclude(product => product!.MeasurementUnit)
            .Include(order => order.Items)
                .ThenInclude(item => item.MasterProduct)
                    .ThenInclude(product => product!.UnitOfMeasure);

    private static string ResolveCustomerItemName(OrderItem item)
    {
        if (item.MasterProduct is null)
        {
            return item.ProductName;
        }

        var preferred = IsArabic() ? item.MasterProduct.NameAr : item.MasterProduct.NameEn;
        var fallback = IsArabic() ? item.MasterProduct.NameEn : item.MasterProduct.NameAr;
        return preferred?.Trim() ?? fallback?.Trim() ?? item.ProductName;
    }

    private static string? BuildVariantDisplaySize(OrderItem item)
    {
        var product = item.MasterProduct;
        if (product is not null)
        {
            var localizedDisplaySize = BuildLocalizedDisplaySize(product);
            if (!string.IsNullOrWhiteSpace(localizedDisplaySize))
            {
                return localizedDisplaySize;
            }
        }

        return NormalizeText(item.SnapshotDisplaySize) ?? NormalizeText(item.UnitName);
    }

    private static string? BuildLocalizedDisplaySize(MasterProduct product)
    {
        var measurementUnit = product.MeasurementUnit ?? product.UnitOfMeasure;
        var displaySize = IsArabic()
            ? MasterProductDisplayDto.BuildDisplaySize(
                product.PackageType?.NameAr,
                product.MeasurementValue,
                measurementUnit?.NameAr,
                measurementUnit?.Symbol,
                true)
            : MasterProductDisplayDto.BuildDisplaySize(
                product.PackageType?.NameEn,
                product.MeasurementValue,
                measurementUnit?.NameEn,
                measurementUnit?.Symbol,
                false);

        return NormalizeText(displaySize);
    }

    private static string? BuildPackageTypeName(OrderItem item)
    {
        var product = item.MasterProduct;
        if (product?.PackageType is null)
        {
            return null;
        }

        return IsArabic()
            ? NormalizeText(product.PackageType.NameAr) ?? NormalizeText(product.PackageType.NameEn)
            : NormalizeText(product.PackageType.NameEn) ?? NormalizeText(product.PackageType.NameAr);
    }

    private static string? BuildMeasurementUnitName(OrderItem item)
    {
        var product = item.MasterProduct;
        var measurementUnit = product?.MeasurementUnit ?? product?.UnitOfMeasure;
        if (measurementUnit is null)
        {
            return null;
        }

        return IsArabic()
            ? NormalizeText(measurementUnit.NameAr) ?? NormalizeText(measurementUnit.NameEn)
            : NormalizeText(measurementUnit.NameEn) ?? NormalizeText(measurementUnit.NameAr);
    }

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

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

    private static OrderSupportCaseDto MapSupportCase(
        OrderSupportCase supportCase,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap) =>
        new(
            supportCase.Id,
            supportCase.OrderId,
            MapSupportCaseType(supportCase.Type),
            ResolveDisplaySupportCaseTypeLabel(supportCase, couponSupportMap),
            MapSupportCaseStatus(ResolveDisplaySupportCaseStatus(supportCase, couponSupportMap)),
            ResolveSupportCaseStatusLabel(ResolveDisplaySupportCaseStatus(supportCase, couponSupportMap)),
            MapSupportCaseQueue(supportCase.Queue),
            ResolveQueueLabel(supportCase.Queue),
            MapSupportCasePriority(supportCase.Priority),
            ResolveSupportCasePriorityLabel(supportCase.Priority),
            supportCase.ReasonCode,
            ResolveSupportCaseReasonLabel(supportCase.Type, supportCase.ReasonCode),
            supportCase.Message,
            supportCase.CustomerVisibleNote,
            supportCase.DecisionNotes,
            supportCase.CreatedAtUtc,
            supportCase.UpdatedAtUtc,
            supportCase.SlaDueAtUtc,
            supportCase.RequestedRefundAmount,
            supportCase.ApprovedRefundAmount,
            supportCase.RefundMethod,
            MapSupportCaseCompensationType(supportCase.CompensationType),
            MapSupportCaseSettlementStatus(supportCase, couponSupportMap),
            ResolveCouponCode(supportCase.CompensationCouponId, couponSupportMap),
            ResolveCouponExpiry(supportCase.CompensationCouponId, couponSupportMap),
            ResolveCouponRedeemed(supportCase.CompensationCouponId, couponSupportMap),
            supportCase.CostBearer,
            supportCase.InitiatorRole,
            ResolveRoleLabel(supportCase.InitiatorRole),
            ResolveDisplayAwaitingResponseRole(supportCase, couponSupportMap),
            ResolveDisplayAwaitingResponseRoleLabel(supportCase, couponSupportMap),
            BuildParticipants(supportCase, couponSupportMap),
            BuildAllowedActions("customer", supportCase),
            supportCase.Attachments
                .Select(attachment => new OrderSupportCaseAttachmentDto(
                    attachment.FileName,
                    attachment.FileUrl))
                .ToList(),
            supportCase.Activities
                .OrderByDescending(activity => activity.CreatedAtUtc)
                .Where(activity => activity.IsVisibleToRole("customer"))
                .Select(activity => new OrderSupportCaseActivityDto(
                    activity.Action,
                    activity.Title,
                    ResolveLocalizedActivityTitle(activity),
                    activity.Note,
                    ResolveLocalizedActivityBody(supportCase, activity),
                    activity.ActorRole,
                    ResolveRoleLabel(activity.ActorRole),
                    activity.VisibleToCustomer,
                    activity.MessageType,
                    ResolveMessageTypeLabel(activity.MessageType),
                    activity.GetVisibleRoles(),
                    activity.IsInternalOnly,
                    activity.CreatedAtUtc))
                .ToList(),
            supportCase.Activities
                .OrderByDescending(activity => activity.CreatedAtUtc)
                .Where(activity => activity.IsVisibleToRole("customer"))
                .Select(activity => new OrderSupportCaseMessageDto(
                    activity.Id,
                    activity.Action,
                    activity.MessageType,
                    activity.Title,
                    ResolveLocalizedActivityTitle(activity),
                    activity.Note,
                    ResolveLocalizedActivityBody(supportCase, activity),
                    activity.ActorRole,
                    ResolveRoleLabel(activity.ActorRole),
                    activity.GetVisibleRoles(),
                    ResolveMessageTypeLabel(activity.MessageType),
                    activity.IsInternalOnly,
                    activity.CreatedAtUtc,
                    []))
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
                supportCase.Order?.OrderNumber ?? string.Empty,
                MapSupportCaseType(supportCase.Type),
                ResolveSupportCaseTypeLabel(supportCase.Type),
                MapSupportCaseStatus(supportCase.Status),
                ResolveSupportCaseStatusLabel(supportCase.Status),
                MapSupportCaseQueue(supportCase.Queue),
                ResolveQueueLabel(supportCase.Queue),
                MapSupportCasePriority(supportCase.Priority),
                ResolveSupportCasePriorityLabel(supportCase.Priority),
                supportCase.ReasonCode,
                ResolveSupportCaseReasonLabel(supportCase.Type, supportCase.ReasonCode),
                supportCase.Message,
                supportCase.CreatedAtUtc,
                supportCase.UpdatedAtUtc);
    }

    private static List<CustomerOrderTrackingTimelineItemDto> BuildTimeline(Order order) =>
        order.Fulfillment == FulfillmentType.Pickup
            ? BuildPickupTimeline(order)
            : BuildDeliveryTimeline(order);

    private static List<CustomerOrderTrackingTimelineItemDto> BuildDeliveryTimeline(Order order)
    {
        var history = order.StatusHistory
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        var isCancelled = order.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed;
        var isReturning = order.Status == OrderStatus.Refunded;
        var terminalId = isCancelled ? "cancelled" : isReturning ? "returning" : "delivered";
        var terminalTitle = isCancelled
            ? L("تم إلغاء الطلب", "Order cancelled")
            : isReturning
                ? L("جاري الإرجاع", "Return in progress")
                : L("تم التسليم", "Delivered");

        var steps = new List<TrackingStepDefinition>
        {
            new("order_placed", L("تم إنشاء الطلب", "Order placed"), GetStepTime(order.PlacedAtUtc), IsCurrentStage(order.Status, TrackingStage.OrderPlaced), IsCompletedStage(order.Status, TrackingStage.OrderPlaced)),
            new("vendor_confirmed", L("أكد المتجر الطلب", "Vendor confirmed"), GetStepTime(ResolveStepDate(history, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.DriverAssignmentInProgress, OrderStatus.DriverAssigned, OrderStatus.PickedUp, OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Refunded)), IsCurrentStage(order.Status, TrackingStage.VendorConfirmed), IsCompletedStage(order.Status, TrackingStage.VendorConfirmed)),
            new("preparing", L("جاري تجهيز الطلب", "Preparing order"), GetStepTime(ResolveStepDate(history, OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.DriverAssignmentInProgress, OrderStatus.DriverAssigned, OrderStatus.PickedUp, OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Refunded)), IsCurrentStage(order.Status, TrackingStage.Preparing), IsCompletedStage(order.Status, TrackingStage.Preparing)),
            new("out_for_delivery", L("في الطريق إليك", "Out for delivery"), GetStepTime(ResolveStepDate(history, OrderStatus.PickedUp, OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Refunded)), IsCurrentStage(order.Status, TrackingStage.OutForDelivery), IsCompletedStage(order.Status, TrackingStage.OutForDelivery))
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

        return MapTimelineSteps(steps);
    }

    private static List<CustomerOrderTrackingTimelineItemDto> BuildPickupTimeline(Order order)
    {
        var history = order.StatusHistory
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        var isCancelled = order.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded;
        var steps = new List<TrackingStepDefinition>
        {
            new(
                "order_placed",
                L("تم إنشاء الطلب", "Order placed"),
                GetStepTime(order.PlacedAtUtc),
                IsPickupCurrentStage(order.Status, TrackingStage.OrderPlaced),
                IsPickupCompletedStage(order.Status, TrackingStage.OrderPlaced)),
            new(
                "vendor_confirmed",
                L("أكد المتجر الطلب", "Vendor confirmed"),
                GetStepTime(ResolveStepDate(history, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.Delivered)),
                IsPickupCurrentStage(order.Status, TrackingStage.VendorConfirmed),
                IsPickupCompletedStage(order.Status, TrackingStage.VendorConfirmed)),
            new(
                "preparing",
                L("جاري تجهيز الطلب", "Preparing order"),
                GetStepTime(ResolveStepDate(history, OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.Delivered)),
                IsPickupCurrentStage(order.Status, TrackingStage.Preparing),
                IsPickupCompletedStage(order.Status, TrackingStage.Preparing)),
            new(
                "ready_for_pickup",
                L("جاهز للاستلام من الفرع", "Ready for pickup"),
                GetStepTime(ResolveStepDate(history, OrderStatus.ReadyForPickup, OrderStatus.Delivered) ?? order.ReadyForPickupAtUtc),
                !isCancelled && IsPickupCurrentStage(order.Status, TrackingStage.ReadyForPickup),
                IsPickupCompletedStage(order.Status, TrackingStage.ReadyForPickup) ||
                (isCancelled && (order.ReadyForPickupAtUtc.HasValue ||
                                 ResolveStepDate(history, OrderStatus.ReadyForPickup).HasValue)))
        };

        if (isCancelled)
        {
            steps.Add(new TrackingStepDefinition(
                "cancelled",
                L("تم إلغاء الطلب", "Order cancelled"),
                GetStepTime(order.CancelledAtUtc ?? ResolveStepDate(history, OrderStatus.Cancelled, OrderStatus.VendorRejected, OrderStatus.DeliveryFailed, OrderStatus.Refunded)),
                true,
                true));
        }
        else
        {
            steps.Add(new TrackingStepDefinition(
                "delivered",
                L("تم الاستلام", "Collected"),
                GetStepTime(order.DeliveredAtUtc ?? ResolveStepDate(history, OrderStatus.Delivered)),
                order.Status == OrderStatus.Delivered,
                order.Status == OrderStatus.Delivered));
        }

        return MapTimelineSteps(steps);
    }

    private static List<CustomerOrderTrackingTimelineItemDto> MapTimelineSteps(IEnumerable<TrackingStepDefinition> steps)
    {
        var mapped = steps
            .Select(step => new CustomerOrderTrackingTimelineItemDto(
                step.Id,
                step.Title,
                step.Time,
                step.IsActive,
                step.IsCompleted))
            .ToList();

        // Contract: exactly one visual "current" step for the mobile timeline design.
        var activeIndexes = mapped
            .Select((step, index) => (step, index))
            .Where(item => item.step.IsActive)
            .Select(item => item.index)
            .ToList();

        if (activeIndexes.Count <= 1)
        {
            return mapped;
        }

        var keepIndex = activeIndexes[^1];
        return mapped
            .Select((step, index) => index == keepIndex
                ? step
                : step with { IsActive = false })
            .ToList();
    }

    private async Task<CustomerOrderEstimatedDeliveryDto?> BuildEstimatedDeliveryAsync(
        Order order, DeliveryAssignment? assignment, CancellationToken cancellationToken)
    {
        if (order.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded)
        {
            return null;
        }

        var preparationTimeMinutes = await _dbContext.Vendors
            .AsNoTracking()
            .Where(v => v.Id == order.VendorId)
            .Select(v => v.PreparationTimeMinutes)
            .FirstOrDefaultAsync(cancellationToken);
        var addressRegion = await _dbContext.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.Id == order.CustomerAddressId)
            .Select(address => new
            {
                address.City,
                address.Area
            })
            .FirstOrDefaultAsync(cancellationToken);
        var operationalProfile = await DeliveryEtaTelemetry.LoadOperationalProfileAsync(
            _dbContext,
            order.VendorId,
            order.VendorBranchId,
            addressRegion?.City,
            addressRegion?.Area,
            cancellationToken);
        var liveSignal = await DeliveryEtaTelemetry.LoadLiveSignalAsync(
            _dbContext,
            order.VendorBranchId,
            cancellationToken);
        var persistedWindow = order.EtaMinMinutes.HasValue &&
                              order.EtaMaxMinutes.HasValue &&
                              !string.IsNullOrWhiteSpace(order.EtaConfidence) &&
                              !string.IsNullOrWhiteSpace(order.EtaSource)
            ? new DeliveryEtaWindow(
                order.EtaMinMinutes.Value,
                order.EtaMaxMinutes.Value,
                order.EtaConfidence!,
                order.EtaSource!,
                order.EtaIsApproximate ?? true,
                order.EtaCalculationMode,
                order.EtaExplanation)
            : null;
        var estimate = DeliveryEtaPolicy.EstimateTracking(
            order.Status,
            order.PlacedAtUtc,
            order.DeliveredAtUtc ?? ResolveHistoryDate(order, OrderStatus.Delivered),
            ResolveHistoryDate(order, OrderStatus.Accepted),
            assignment?.AcceptedAtUtc,
            assignment?.PickedUpAtUtc,
            preparationTimeMinutes,
            order.DriverToVendorDistanceKm,
            order.VendorToCustomerDistanceKm,
            operationalProfile,
            liveSignal,
            persistedWindow);

        if (estimate is null)
        {
            return null;
        }

        var estimatedAtUtc = estimate.DatetimeUtc;
        var estimatedAtSaudi = SaudiTime.ToSaudi(estimatedAtUtc);
        return new CustomerOrderEstimatedDeliveryDto(
            estimatedAtUtc,
            estimatedAtSaudi.ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture),
            estimate.Window is null
                ? null
                : new EstimatedDeliveryWindowDto(
                    estimate.Window.MinMinutes,
                    estimate.Window.MaxMinutes,
                    estimate.Window.Confidence,
                    estimate.Window.Source,
                    estimate.Window.IsApproximate,
                    estimate.Window.CalculationMode,
                    estimate.Window.Explanation));
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
            string.IsNullOrWhiteSpace(assignment.Driver.LicenseNumber) ? "N/A" : assignment.Driver.LicenseNumber,
            assignment.Driver.PersonalPhotoUrl ?? assignment.Driver.User.ProfilePhotoUrl);
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
            OrderStatus.PendingPayment or OrderStatus.PendingBankConfirmation or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => "pending",
            OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.ReadyForPickup or
            OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or
            OrderStatus.PickedUp or OrderStatus.OnTheWay => "processing",
            OrderStatus.Delivered or OrderStatus.Refunded => "delivered",
            _ => "cancelled"
        };

    private static string MapCustomerFacingStatus(Order order) =>
        order.Fulfillment == FulfillmentType.Pickup
            ? order.Status switch
            {
                OrderStatus.PendingPayment or OrderStatus.PendingBankConfirmation or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => "pending",
                OrderStatus.Accepted => "accepted",
                OrderStatus.Preparing => "preparing",
                OrderStatus.ReadyForPickup or
                OrderStatus.DriverAssignmentInProgress or
                OrderStatus.DriverAssigned or
                OrderStatus.PickedUp or
                OrderStatus.OnTheWay => "ready_for_pickup",
                OrderStatus.Delivered => "delivered",
                OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded => "cancelled",
                _ => "cancelled"
            }
            : MapStatus(order.Status);

    private static string MapTrackingStatus(Order order) =>
        OrderTrackingStatusMapper.ToCustomerTrackingStatus(order.Status, order.Fulfillment);

    private static string MapFulfillmentType(FulfillmentType fulfillment) =>
        fulfillment == FulfillmentType.Pickup ? "pickup" : "delivery";

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
            OrderSupportCaseType.DriverReport => "driver_report",
            OrderSupportCaseType.DriverDispute => "driver_dispute",
            OrderSupportCaseType.DriverAccountAppeal => "driver_account",
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

    private static string ResolveSupportCaseTypeLabel(OrderSupportCaseType type) =>
        type switch
        {
            OrderSupportCaseType.ReturnRequest => L("طلب استرجاع", "Return request"),
            OrderSupportCaseType.DriverReport => L("بلاغ تشغيلي", "Operational report"),
            OrderSupportCaseType.DriverDispute => L("نزاع مالي", "Financial dispute"),
            OrderSupportCaseType.DriverAccountAppeal => L("دعم حساب المندوب", "Driver account support"),
            _ => L("شكوى", "Complaint")
        };

    private static string ResolveSupportCaseStatusLabel(OrderSupportCaseStatus status) =>
        status switch
        {
            OrderSupportCaseStatus.Submitted => L("استلمنا", "Submitted"),
            OrderSupportCaseStatus.InReview => L("قيد المراجعة", "In review"),
            OrderSupportCaseStatus.AwaitingCustomerEvidence => L("بانتظار معلومات إضافية", "Awaiting more evidence"),
            OrderSupportCaseStatus.Approved => L("اعتمدنا", "Approved"),
            OrderSupportCaseStatus.Rejected => L("رفضنا", "Rejected"),
            _ => L("حلّينا", "Resolved")
        };

    private static string ResolveAdminSupportCaseStatusLabel(OrderSupportCaseStatus status) =>
        status switch
        {
            OrderSupportCaseStatus.Submitted => L("مفتوحة", "Open"),
            OrderSupportCaseStatus.InReview => L("قيد المراجعة", "Under review"),
            OrderSupportCaseStatus.AwaitingCustomerEvidence => L("بانتظار رد", "Awaiting response"),
            OrderSupportCaseStatus.Approved => L("معتمدة", "Approved"),
            OrderSupportCaseStatus.Rejected => L("مرفوضة", "Rejected"),
            _ => L("مغلقة", "Resolved")
        };

    private static string ResolveSupportCasePriorityLabel(OrderSupportCasePriority priority) =>
        priority switch
        {
            OrderSupportCasePriority.Low => L("منخفضة", "Low"),
            OrderSupportCasePriority.Medium => L("متوسطة", "Medium"),
            OrderSupportCasePriority.High => L("مرتفعة", "High"),
            _ => L("حرجة", "Critical")
        };

    private static string? ResolveSupportCaseReasonLabel(OrderSupportCaseType type, string? reasonCode)
    {
        var reason = OrderSupportCaseReasonCatalog.FindReason(MapSupportCaseType(type), reasonCode);
        return reason is null ? null : L(reason.LabelAr, reason.LabelEn);
    }

    private static string? ResolveLocalizedActivityTitle(OrderSupportCaseActivity activity)
    {
        return NormalizeSupportCaseAction(activity.Action) switch
        {
            "submitted" => L("فتحنا الحالة", "Case opened"),
            "driver_response" => L("رد المندوب", "Driver replied"),
            "vendor_response" => L("رد التاجر", "Vendor replied"),
            "customer_response" => L("رد العميل", "Customer replied"),
            "request_evidence" => L("طلب معلومات إضافية", "More evidence requested"),
            "assigned" => L("أسندنا", "Case assigned"),
            "escalated" => L("صعّدنا", "Case escalated"),
            "approved" => L("اعتمدنا", "Case approved"),
            "rejected" => L("رفضنا", "Case rejected"),
            "resolved" => L("حلّينا", "Case resolved"),
            "reopened" => L("أعيد فتح الحالة", "Case reopened"),
            "admin_message" => L("رسالة من الإدارة", "Admin update"),
            "internal_note" => L("ملاحظة داخلية", "Internal note"),
            "customer_note" => L("ملاحظة عامة", "Public note"),
            _ => activity.Title
        };
    }

    private static string? ResolveLocalizedActivityBody(OrderSupportCase supportCase, OrderSupportCaseActivity activity)
    {
        var orderNumber = supportCase.Order?.OrderNumber ?? supportCase.OrderId.ToString();
        return NormalizeSupportCaseAction(activity.Action) switch
        {
            "submitted" => supportCase.Type == OrderSupportCaseType.ReturnRequest
                ? L(
                    $"استلمنا طلب الاسترجاع للطلب رقم {orderNumber} وهو الآن تحت المراجعة.",
                    $"We received the return request for order #{orderNumber} and it is now under review.")
                : L(
                    $"استلمنا الحالة المرتبطة بالطلب رقم {orderNumber} وهي الآن تحت المراجعة.",
                    $"We received the support case for order #{orderNumber} and it is now under review."),
            "request_evidence" => L(
                $"نحتاج إلى معلومات أو أدلة إضافية لمتابعة مراجعة الحالة الخاصة بالطلب رقم {orderNumber}.",
                $"We need additional information or evidence to continue reviewing the case for order #{orderNumber}."),
            "approved" => supportCase.Type == OrderSupportCaseType.ReturnRequest
                ? L(
                    "اعتمدنا طلب الاسترجاع وراح نبلغك عند بدء المعالجة المالية.",
                    "Your return request has been approved. You will be notified when the financial processing begins.")
                : L(
                    $"اعتمدنا الحالة الخاصة بالطلب رقم {orderNumber}.",
                    $"The support case linked to order #{orderNumber} has been approved."),
            "rejected" => L(
                $"رفضنا الحالة الخاصة بالطلب رقم {orderNumber}.",
                $"The support case linked to order #{orderNumber} has been rejected."),
            "resolved" => L(
                $"أغلقنا الحالة الخاصة بالطلب رقم {orderNumber} بعد معالجتها.",
                $"The support case linked to order #{orderNumber} has been resolved and closed."),
            "reopened" => L(
                $"أعيد فتح الحالة الخاصة بالطلب رقم {orderNumber} لمراجعتها مرة أخرى.",
                $"The support case linked to order #{orderNumber} was reopened for another review."),
            "escalated" => L(
                $"صعّدنا الحالة الخاصة بالطلب رقم {orderNumber} إلى فريق مختص.",
                $"The support case linked to order #{orderNumber} was escalated to a specialized team."),
            _ => activity.Note
        };
    }

    private static string NormalizeSupportCaseAction(string? action) =>
        string.IsNullOrWhiteSpace(action) ? string.Empty : action.Trim().ToLowerInvariant();

    private static string? MapSupportCaseCompensationType(OrderSupportCaseCompensationType? compensationType) =>
        compensationType switch
        {
            OrderSupportCaseCompensationType.CashRefund => "cash_refund",
            OrderSupportCaseCompensationType.CouponCompensation => "coupon_compensation",
            _ => null
        };

    private static string? MapSupportCaseSettlementStatus(
        OrderSupportCase supportCase,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap)
    {
        if (supportCase.Type != OrderSupportCaseType.ReturnRequest)
        {
            return null;
        }

        return supportCase.Status switch
        {
            OrderSupportCaseStatus.Submitted or
            OrderSupportCaseStatus.InReview or
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "pending_review",
            OrderSupportCaseStatus.Rejected => "rejected",
            OrderSupportCaseStatus.Approved or
            OrderSupportCaseStatus.Resolved => supportCase.CompensationType switch
            {
                OrderSupportCaseCompensationType.CashRefund => "cash_refunded",
                OrderSupportCaseCompensationType.CouponCompensation when ResolveCouponRedeemed(supportCase.CompensationCouponId, couponSupportMap) => "coupon_redeemed",
                OrderSupportCaseCompensationType.CouponCompensation => "coupon_issued",
                _ => supportCase.ApprovedRefundAmount.HasValue ? "approved" : null
            },
            _ => null
        };
    }

    private static string? MapVendorRecoveryStatus(VendorRecoveryStatus? status) =>
        status switch
        {
            VendorRecoveryStatus.Pending => "pending",
            VendorRecoveryStatus.PartiallyRecovered => "partial",
            VendorRecoveryStatus.Recovered => "recovered",
            _ => null
        };

    private static string MapPaymentMethod(PaymentMethodType paymentMethod) =>
        paymentMethod switch
        {
            PaymentMethodType.Card => "card",
            PaymentMethodType.BankTransfer => "bank",
            PaymentMethodType.Wallet => "wallet",
            PaymentMethodType.ApplePay => "apple_pay",
            PaymentMethodType.Mada => "mada",
            _ => "cash"
        };

    private static bool IsCurrentStage(OrderStatus status, TrackingStage stage) =>
        stage switch
        {
            TrackingStage.OrderPlaced => status is OrderStatus.PendingPayment or OrderStatus.Placed,
            // Waiting for merchant acceptance only — once Accepted, preparing becomes current.
            TrackingStage.VendorConfirmed => status is OrderStatus.PendingVendorAcceptance,
            TrackingStage.Preparing => status is OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned,
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

    private static bool IsPickupReadyLike(OrderStatus status) =>
        status is OrderStatus.ReadyForPickup
            or OrderStatus.DriverAssignmentInProgress
            or OrderStatus.DriverAssigned
            or OrderStatus.PickedUp
            or OrderStatus.OnTheWay;

    private static bool IsPickupCurrentStage(OrderStatus status, TrackingStage stage) =>
        stage switch
        {
            TrackingStage.OrderPlaced => status is OrderStatus.PendingPayment or OrderStatus.Placed,
            TrackingStage.VendorConfirmed => status is OrderStatus.PendingVendorAcceptance,
            TrackingStage.Preparing => status is OrderStatus.Accepted or OrderStatus.Preparing,
            TrackingStage.ReadyForPickup => IsPickupReadyLike(status),
            _ => false
        };

    private static bool IsPickupCompletedStage(OrderStatus status, TrackingStage stage) =>
        stage switch
        {
            TrackingStage.OrderPlaced => status is not (OrderStatus.PendingPayment or OrderStatus.Placed),
            TrackingStage.VendorConfirmed =>
                IsPickupReadyLike(status) ||
                status is OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.Delivered
                    or OrderStatus.Cancelled or OrderStatus.VendorRejected
                    or OrderStatus.DeliveryFailed or OrderStatus.Refunded,
            TrackingStage.Preparing =>
                IsPickupReadyLike(status) ||
                status is OrderStatus.Delivered or OrderStatus.Cancelled
                    or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded,
            TrackingStage.ReadyForPickup => status == OrderStatus.Delivered,
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
            ? SaudiTime.ToSaudi(dateTimeUtc.Value).ToString("hh:mm tt", CultureInfo.InvariantCulture)
            : string.Empty;

    private static IReadOnlyList<VendorOrderTimelineItemDto> BuildVendorTimeline(Order order)
    {
        var history = order.StatusHistory
            .OrderBy(entry => entry.CreatedAtUtc)
            .ToList();

        var timeline = new List<VendorOrderTimelineItemDto>
        {
            new(
                OrderStatus.PendingVendorAcceptance.ToString(),
                "Order placed",
                order.PlacedAtUtc,
                true,
                null)
        };

        timeline.AddRange(history.Select(entry => new VendorOrderTimelineItemDto(
            entry.NewStatus.ToString(),
            entry.NewStatus.ToString(),
            entry.CreatedAtUtc,
            true,
            entry.Note)));

        return timeline
            .GroupBy(item => item.Status, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(item => item.TimestampUtc).First())
            .OrderBy(item => item.TimestampUtc)
            .ToList();
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

    private sealed record AdminAddressSnapshot(string AddressLine, string City, string Area, string ContactPhone, decimal? Latitude, decimal? Longitude);

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
                        address.ContactPhone ?? string.Empty,
                        address.Latitude,
                        address.Longitude)
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
                    ? L("لا يوجد GPS", "No GPS")
                    : evaluation.GpsFresh
                        ? L("مباشر الآن", "Live now")
                        : L($"منذ {Math.Max(1, (int)(utcNow - latestLocation.RecordedAtUtc).TotalMinutes)} دقيقة", $"{Math.Max(1, (int)(utcNow - latestLocation.RecordedAtUtc).TotalMinutes)}m ago");

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
                    LocalizeCity(driver.City ?? dispatchContext.PickupCity ?? L("غير معروف", "Unknown")),
                    driver.Address ?? L("منطقة التغطية", "Coverage area"),
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
        IReadOnlyList<AdminDriverCandidateDto> driverCandidates,
        DriverLiveLocationDto? driverLiveLocation = null,
        PickupBranchDto? pickupBranch = null,
        IReadOnlyList<CustomerAddressOptionDto>? customerAddresses = null)
    {
        var baseItem = BuildAdminOrderListItem(order, address, payment, refunds, assignment);
        var operationalCase = BuildOperationalCase(order, refunds);
        var placedAtLocal = SaudiTime.ToSaudi(order.PlacedAtUtc);
        var merchantLocation = string.Join(", ", new[] { order.VendorBranch?.AddressLine, LocalizeCity(order.Vendor?.City), order.Vendor?.NationalAddress }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        var timeline = BuildAdminTimeline(order, payment, assignment, operationalCase);
        var activities = BuildAdminActivities(order, payment, refunds, assignment, operationalCase);
        var candidateScoreBreakdown = BuildCandidateScoreBreakdown(assignment, driverCandidates);
        var customerPickupOtpStatus = order.Fulfillment != FulfillmentType.Pickup
            ? "not_applicable"
            : order.PickupOtpVerifiedAtUtc.HasValue
                ? "verified"
                : order.PickupOtpLockedUntilUtc.HasValue && order.PickupOtpLockedUntilUtc > DateTime.UtcNow
                    ? "locked"
                    : order.Status == OrderStatus.ReadyForPickup
                        ? "pending"
                        : "not_available";

        return new AdminOrderDetailDto(
            order.Id,
            baseItem.DisplayId,
            order.User.FullName,
            address?.ContactPhone ?? order.User.PhoneNumber ?? string.Empty,
            order.User.Email ?? string.Empty,
            BuildCustomerAddress(address),
            order.UserId,
            order.Vendor?.BusinessNameAr ?? string.Empty,
            order.VendorBranch?.Name ?? L("الفرع الرئيسي", "Main branch"),
            merchantLocation,
            assignment?.DriverId?.ToString(),
            assignment?.Driver?.User?.FullName ?? string.Empty,
            assignment?.Driver?.User?.PhoneNumber ?? string.Empty,
            assignment?.Driver?.VehicleType?.ToString() ?? L("مركبة توصيل", "Delivery vehicle"),
            assignment?.Driver?.LicenseNumber ?? "N/A",
            LocalizeCity(address?.City ?? order.Vendor?.City ?? string.Empty),
            address?.Area ?? string.Empty,
            CalculateSlaScore(order, assignment),
            placedAtLocal.ToString("yyyy-MM-dd"),
            placedAtLocal.ToString("hh:mm tt", CultureInfo.InvariantCulture),
            baseItem.Status,
            baseItem.PaymentStatus,
            baseItem.FulfillmentStatus,
            order.Fulfillment.ToString(),
            customerPickupOtpStatus,
            order.PickupOtpFailedAttempts,
            order.PickupOtpLockedUntilUtc,
            order.PickupNoShowDeadlineUtc,
            pickupBranch,
            customerAddresses ?? Array.Empty<CustomerAddressOptionDto>(),
            baseItem.DispatchState,
            baseItem.DispatchReasonAr,
            baseItem.DispatchReasonEn,
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
            BuildDeliveryBreakdown(order),
            Math.Max(0, order.TotalAmount - order.Subtotal - order.DeliveryFee),
            order.TotalAmount,
            address?.Latitude is not null && address?.Longitude is not null
                ? new GeoPointDto(address.Latitude.Value, address.Longitude.Value) : null,
            order.VendorBranch is not null
                ? new GeoPointDto(order.VendorBranch.Latitude, order.VendorBranch.Longitude) : null,
            driverLiveLocation,
            order.Items.Select(item => new AdminOrderItemDto(
                item.MasterProduct is not null
                    ? (item.MasterProduct.NameAr == item.MasterProduct.NameEn
                        ? (item.MasterProduct.NameAr ?? item.ProductName)
                        : $"{item.MasterProduct.NameAr} / {item.MasterProduct.NameEn}")
                    : item.ProductName,
                item.MasterProduct?.NameAr ?? item.ProductName,
                item.MasterProduct?.NameEn ?? item.ProductName,
                "General",
                item.Quantity.ToString(CultureInfo.InvariantCulture),
                item.UnitPrice,
                item.LineTotal,
                "inventory_2",
                item.MasterProductId == Guid.Empty ? item.Id.ToString("N")[..8].ToUpperInvariant() : item.MasterProductId.ToString("N")[..8].ToUpperInvariant(),
                BuildProductImageUrl(item),
                BuildVariantDisplaySize(item),
                BuildPackageTypeName(item),
                item.MasterProduct?.MeasurementValue,
                BuildMeasurementUnitName(item)))
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
        var placedAtLocal = SaudiTime.ToSaudi(order.PlacedAtUtc);
        var isLate = IsLate(order.Status, order.PlacedAtUtc);
        var operationalCase = BuildOperationalCase(order, refunds);
        var dispatchReason = BuildDispatchReason(order, assignment);

        return new AdminOrderListItemDto(
            order.Id,
            $"#{order.OrderNumber}",
            order.User.FullName,
            address?.ContactPhone ?? order.User.PhoneNumber ?? string.Empty,
            order.Vendor.BusinessNameAr,
            order.VendorBranch?.Name ?? L("الفرع الرئيسي", "Main branch"),
            placedAtLocal.ToString("yyyy-MM-dd"),
            placedAtLocal.ToString("hh:mm tt", CultureInfo.InvariantCulture),
            MapAdminStatus(order.Status),
            MapAdminPaymentStatus(order.PaymentStatus, refunds),
            MapFulfillmentStatus(order.Status, assignment),
            BuildDispatchState(order.Status, assignment),
            dispatchReason.Ar,
            dispatchReason.En,
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
        string? queue,
        string? initiatorRole)
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
            !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            var matchesStatus =
                string.Equals(item.CaseStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase) ||
                normalizedStatus switch
                {
                    "active" => !string.Equals(item.CaseStatus, "resolved", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(item.CaseStatus, "rejected", StringComparison.OrdinalIgnoreCase),
                    "review" => string.Equals(item.CaseStatus, "in_review", StringComparison.OrdinalIgnoreCase),
                    "merchant" => string.Equals(item.CaseStatus, "awaiting_customer_evidence", StringComparison.OrdinalIgnoreCase),
                    "open" => string.Equals(item.CaseStatus, "submitted", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };

            if (!matchesStatus)
            {
                return false;
            }
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

        if (!string.IsNullOrWhiteSpace(initiatorRole) &&
            !string.Equals(initiatorRole, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.InitiatorRole, initiatorRole, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static IReadOnlyList<OrderSupportCaseParticipantDto> BuildParticipants(
        OrderSupportCase supportCase,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap)
    {
        var waitingOnRole = ResolveDisplayAwaitingResponseRole(supportCase, couponSupportMap);
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "customer",
            "vendor"
        };

        if (supportCase.Type is OrderSupportCaseType.DriverReport or OrderSupportCaseType.DriverDispute or OrderSupportCaseType.DriverAccountAppeal ||
            supportCase.Activities.Any(activity => string.Equals(activity.ActorRole, "driver", StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrWhiteSpace(supportCase.DriverResponse))
        {
            roles.Add("driver");
        }

        return roles
            .Select(role => new OrderSupportCaseParticipantDto(
                role,
                ResolveRoleLabel(role),
                string.Equals(supportCase.InitiatorRole, role, StringComparison.OrdinalIgnoreCase),
                string.Equals(waitingOnRole, role, StringComparison.OrdinalIgnoreCase),
                supportCase.Activities.Any(activity => string.Equals(activity.ActorRole, role, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    private static IReadOnlyList<string> BuildAllowedActions(string viewerRole, OrderSupportCase supportCase)
    {
        if (supportCase.Status is OrderSupportCaseStatus.Rejected or OrderSupportCaseStatus.Resolved)
        {
            var reopenCount = supportCase.Activities.Count(a => a.Action == "reopened");
            return viewerRole == "admin" && reopenCount < 3 ? ["reopen"] : [];
        }

        return viewerRole switch
        {
            "admin" when supportCase.Status == OrderSupportCaseStatus.Approved => ["note", "message", "resolve"],
            "admin" => ["assign", "note", "message", "request_evidence", "escalate", "approve", "reject", "resolve"],
            "vendor" => ["message"],
            "driver" => ["message"],
            "customer" => ["message"],
            _ => []
        };
    }

    private static AdminOrderSupportCaseListItemDto BuildAdminSupportCaseListItem(
        OrderSupportCase supportCase,
        Payment? payment,
        IReadOnlyList<Refund>? refunds,
        VendorRecovery? recovery,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap)
    {
        var order = supportCase.Order;
        var amount = supportCase.ApprovedRefundAmount
            ?? supportCase.RequestedRefundAmount
            ?? refunds?.OrderByDescending(item => item.CreatedAtUtc).FirstOrDefault()?.Amount
            ?? order?.TotalAmount
            ?? 0m;

        var createdAt = SaudiTime.ToSaudi(supportCase.CreatedAtUtc).ToString("g", CultureInfo.InvariantCulture);
        var sla = supportCase.SlaDueAtUtc.HasValue
            ? SaudiTime.ToSaudi(supportCase.SlaDueAtUtc.Value).ToString("g", CultureInfo.InvariantCulture)
            : "No SLA";

        return new AdminOrderSupportCaseListItemDto(
            supportCase.Id,
            order?.Id,
            order?.OrderNumber ?? L("حساب مندوب", "Driver account"),
            order?.User.FullName ?? "Driver account",
            order?.User.Email ?? string.Empty,
            order?.Vendor.BusinessNameAr ?? "Driver operations",
            MapSupportCaseType(supportCase.Type),
            ResolveDisplaySupportCaseTypeLabel(supportCase, couponSupportMap),
            supportCase.ReasonCode,
            ResolveSupportCaseReasonLabel(supportCase.Type, supportCase.ReasonCode) ?? supportCase.Message,
            amount,
            MapSupportCaseStatus(ResolveDisplaySupportCaseStatus(supportCase, couponSupportMap)),
            ResolveSupportCaseStatusLabel(ResolveDisplaySupportCaseStatus(supportCase, couponSupportMap)),
            MapAdminSupportCaseStatus(ResolveDisplaySupportCaseStatus(supportCase, couponSupportMap)),
            ResolveAdminSupportCaseStatusLabel(ResolveDisplaySupportCaseStatus(supportCase, couponSupportMap)),
            MapSupportCasePriority(supportCase.Priority),
            ResolveSupportCasePriorityLabel(supportCase.Priority),
            supportCase.AssignedAdminId.HasValue ? L("مراجع مسند", "Assigned admin") : ResolveQueueLabel(supportCase.Queue),
            MapSupportCaseQueue(supportCase.Queue),
            ResolveQueueLabel(supportCase.Queue),
            MapRiskLevel(supportCase.Priority),
            createdAt,
            sla,
            supportCase.CustomerVisibleNote ?? supportCase.DecisionNotes ?? supportCase.Message,
            order is null ? "account" : MapPaymentMethod(order.PaymentMethod),
            order is null ? "N/A" : BuildPaymentMask(payment, order),
            order is null ? BuildDriverAccountSummary(supportCase) : BuildCustomerSummary(order, supportCase),
            order is null ? "Driver operations is handling this account support case." : BuildMerchantSummary(order, supportCase),
            MapSupportCaseCompensationType(supportCase.CompensationType),
            MapSupportCaseSettlementStatus(supportCase, couponSupportMap),
            MapVendorRecoveryStatus(recovery?.Status),
            recovery?.RecoveredAmount ?? 0m,
            recovery?.OutstandingAmount ?? 0m,
            ResolveCouponCode(supportCase.CompensationCouponId, couponSupportMap),
            ResolveCouponExpiry(supportCase.CompensationCouponId, couponSupportMap),
            ResolveCouponRedeemed(supportCase.CompensationCouponId, couponSupportMap),
            supportCase.Attachments
                .Select(attachment => new OrderSupportCaseAttachmentDto(
                    attachment.FileName,
                    attachment.FileUrl))
                .ToList(),
            supportCase.Activities
                .OrderByDescending(activity => activity.CreatedAtUtc)
                .Take(6)
                .Select(activity => new AdminOrderSupportCaseTimelineItemDto(
                    ResolveLocalizedActivityTitle(activity) ?? activity.Title,
                    SaudiTime.ToSaudi(activity.CreatedAtUtc).ToString("g", CultureInfo.InvariantCulture),
                    ResolveTimelineTone(activity.Action, supportCase.Status)))
                .ToList(),
            supportCase.InitiatorRole,
            ResolveRoleLabel(supportCase.InitiatorRole),
            ResolveDisplayAwaitingResponseRole(supportCase, couponSupportMap),
            ResolveDisplayAwaitingResponseRoleLabel(supportCase, couponSupportMap),
            BuildParticipants(supportCase, couponSupportMap),
            BuildAllowedActions("admin", supportCase),
            supportCase.Activities
                .OrderByDescending(activity => activity.CreatedAtUtc)
                .Select(activity => new OrderSupportCaseMessageDto(
                    activity.Id,
                    activity.Action,
                    activity.MessageType,
                    activity.Title,
                    ResolveLocalizedActivityTitle(activity),
                    activity.Note,
                    ResolveLocalizedActivityBody(supportCase, activity),
                    activity.ActorRole,
                    ResolveRoleLabel(activity.ActorRole),
                    activity.GetVisibleRoles(),
                    ResolveMessageTypeLabel(activity.MessageType),
                    activity.IsInternalOnly,
                    activity.CreatedAtUtc,
                    []))
                .ToList(),
            supportCase.VendorResponse,
            supportCase.DriverResponse);
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

    private static OrderSupportCaseStatus ResolveDisplaySupportCaseStatus(
        OrderSupportCase supportCase,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap)
    {
        if (supportCase.Status != OrderSupportCaseStatus.Approved)
        {
            return supportCase.Status;
        }

        var settlementStatus = MapSupportCaseSettlementStatus(supportCase, couponSupportMap);
        return settlementStatus is "cash_refunded" or "coupon_redeemed"
            ? OrderSupportCaseStatus.Resolved
            : supportCase.Status;
    }

    private static string? ResolveDisplayAwaitingResponseRole(
        OrderSupportCase supportCase,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap) =>
        ResolveDisplaySupportCaseStatus(supportCase, couponSupportMap) == OrderSupportCaseStatus.Resolved
            ? null
            : supportCase.AwaitingResponseFromRole;

    private static string ResolveDisplayAwaitingResponseRoleLabel(
        OrderSupportCase supportCase,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap) =>
        ResolveDisplaySupportCaseStatus(supportCase, couponSupportMap) == OrderSupportCaseStatus.Resolved
            ? L("لا يوجد انتظار", "No pending party")
            : ResolveRoleLabel(supportCase.AwaitingResponseFromRole);

    private static string ResolveDisplaySupportCaseTypeLabel(
        OrderSupportCase supportCase,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap)
    {
        if (ResolveDisplaySupportCaseStatus(supportCase, couponSupportMap) != OrderSupportCaseStatus.Resolved)
        {
            return ResolveSupportCaseTypeLabel(supportCase.Type);
        }

        return supportCase.Type switch
        {
            OrderSupportCaseType.ReturnRequest => L("طلب إرجاع مغلق", "Closed return request"),
            OrderSupportCaseType.DriverReport => L("بلاغ مندوب مغلق", "Closed driver report"),
            OrderSupportCaseType.DriverDispute => L("نزاع مندوب مغلق", "Closed driver dispute"),
            _ => L("شكوى مغلقة", "Closed complaint")
        };
    }

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

    private static string? ResolveCouponCode(
        Guid? couponId,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap) =>
        couponId.HasValue && couponSupportMap.TryGetValue(couponId.Value, out var coupon)
            ? coupon.Code
            : null;

    private static DateTime? ResolveCouponExpiry(
        Guid? couponId,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap) =>
        couponId.HasValue && couponSupportMap.TryGetValue(couponId.Value, out var coupon)
            ? coupon.ExpiresAtUtc
            : null;

    private static bool ResolveCouponRedeemed(
        Guid? couponId,
        IReadOnlyDictionary<Guid, CouponSupportSnapshot> couponSupportMap) =>
        couponId.HasValue &&
        couponSupportMap.TryGetValue(couponId.Value, out var coupon) &&
        coupon.IsRedeemed;

    private static string BuildCustomerSummary(Order order, OrderSupportCase supportCase) =>
        L(
            $"'D9EJD {order.User.FullName} A*- {ResolveSupportCaseTypeLabel(supportCase.Type)} DD7D( 1BE {order.OrderNumber}.",
            $"Customer {order.User.FullName} opened a {ResolveSupportCaseTypeLabel(supportCase.Type).ToLowerInvariant()} for order {order.OrderNumber}.");

    private static string BuildMerchantSummary(Order order, OrderSupportCase supportCase) =>
        L(
            $"'D*',1 {order.Vendor.BusinessNameAr} J*E 'D*9'ED E9 'D-'D) 'D.'5) (G -'DJK' 9(1 E3'1 {ResolveQueueLabel(supportCase.Queue)}.",
            $"Merchant {order.Vendor.BusinessNameAr} is currently handled through the {ResolveQueueLabel(supportCase.Queue)} queue.");

    private static string BuildDriverAccountSummary(OrderSupportCase supportCase) =>
        L(
            $"المندوب فتح طلب دعم لحسابه عبر مسار {ResolveQueueLabel(supportCase.Queue)}.",
            $"Driver opened an account support case through the {ResolveQueueLabel(supportCase.Queue)} queue.");

    private async Task<Dictionary<Guid, CouponSupportSnapshot>> LoadCouponSupportMapAsync(
        IReadOnlyCollection<Guid> couponIds,
        CancellationToken cancellationToken)
    {
        if (couponIds.Count == 0)
        {
            return [];
        }

        var coupons = await _dbContext.Coupons
            .AsNoTracking()
            .Where(coupon => couponIds.Contains(coupon.Id))
            .Select(coupon => new
            {
                coupon.Id,
                coupon.Code,
                coupon.EndsAtUtc
            })
            .ToListAsync(cancellationToken);

        var redeemedCouponIds = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CouponId.HasValue && couponIds.Contains(order.CouponId.Value))
            .Select(order => order.CouponId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var redeemedLookup = redeemedCouponIds.ToHashSet();

        return coupons.ToDictionary(
            coupon => coupon.Id,
            coupon => new CouponSupportSnapshot(
                coupon.Code,
                coupon.EndsAtUtc,
                redeemedLookup.Contains(coupon.Id)));
    }

    private async Task<Dictionary<Guid, VendorRecovery>> LoadVendorRecoveryMapAsync(
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken cancellationToken)
    {
        if (caseIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.VendorRecoveries
            .AsNoTracking()
            .Where(item => caseIds.Contains(item.OrderSupportCaseId))
            .ToDictionaryAsync(item => item.OrderSupportCaseId, cancellationToken);
    }

    private static string ResolveTimelineTone(string action, OrderSupportCaseStatus status)
    {
        if (action is "approved" or "resolved")
        {
            return "warning";
        }

        return status == OrderSupportCaseStatus.AwaitingCustomerEvidence ? "muted" : "primary";
    }

    private static string ResolveRoleLabel(string? role) =>
        NormalizeSupportCaseAction(role) switch
        {
            "admin" => L("الإدارة", "Admin"),
            "vendor" => L("التاجر", "Vendor"),
            "driver" => L("المندوب", "Driver"),
            "customer" => L("العميل", "Customer"),
            _ => L("النظام", "System")
        };

    private static string ResolveMessageTypeLabel(string? messageType) =>
        NormalizeSupportCaseAction(messageType) switch
        {
            "decision" => L("قرار", "Decision"),
            "case_opened" => L("فتح الحالة", "Case opened"),
            "public_message" => L("رسالة عامة", "Public message"),
            "internal_note" => L("ملاحظة داخلية", "Internal note"),
            "customer_note" => L("ملاحظة للعميل", "Customer note"),
            "request_evidence" => L("طلب معلومات إضافية", "Evidence request"),
            _ => L("تحديث", "Update")
        };

    private sealed record CouponSupportSnapshot(
        string Code,
        DateTime? ExpiresAtUtc,
        bool IsRedeemed);

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
            OrderSupportCaseQueue.Finance => L("المالية", "Finance"),
            OrderSupportCaseQueue.Operations => L("العمليات", "Operations"),
            OrderSupportCaseQueue.Risk => L("المخاطر", "Risk"),
            OrderSupportCaseQueue.Legal => L("القانونية", "Legal"),
            _ => L("الدعم", "Support")
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

    private static LocalizedText BuildDispatchReason(Order order, DeliveryAssignment? assignment)
    {
        if (!string.IsNullOrWhiteSpace(assignment?.FailureReason))
        {
            return OrderStatusNoteCatalog.LocalizeNote(assignment.FailureReason);
        }

        var latestDispatchNote = order.StatusHistory
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault(item =>
                item.NewStatus is OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned)
            ?.Note;

        if (!string.IsNullOrWhiteSpace(latestDispatchNote))
        {
            return OrderStatusNoteCatalog.LocalizeNote(latestDispatchNote);
        }

        return order.Status switch
        {
            OrderStatus.ReadyForPickup => new("الطلب جاهز وبانتظار التوجيه.", "Order is ready and waiting for dispatch."),
            OrderStatus.DriverAssignmentInProgress => new("قائمة التوجيه تبحث عن أفضل مندوب متاح.", "Dispatch queue is searching for the best available driver."),
            OrderStatus.DriverAssigned => assignment?.Driver?.User is not null
                ? new($"عيّنا إلى {assignment.Driver.User.FullName}.", $"Assigned to {assignment.Driver.User.FullName}.")
                : new("عيّنا المندوب.", "Driver assignment completed."),
            OrderStatus.PickedUp => new("المندوب استلم الطلب.", "Driver picked up the order."),
            OrderStatus.OnTheWay => new("المندوب في الطريق إلى العميل.", "Driver is on the way to the customer."),
            OrderStatus.Delivered => new("وصلنا بنجاح.", "Delivery completed successfully."),
            OrderStatus.DeliveryFailed => new("محاولة التوصيل فشلت وتحتاج تدخل.", "Delivery attempt failed and needs intervention."),
            _ => new("التوجيه غير نشط للحالة الحالية.", "Dispatch is not active for the current order state.")
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
            L($"سبب التطابق: {matchedCandidate.DispatchMatchReason}", $"Match reason: {matchedCandidate.DispatchMatchReason}"),
            L($"درجة الالتزام: {matchedCandidate.CommitmentScore:0.0}", $"Commitment score: {matchedCandidate.CommitmentScore:0.0}"),
            L($"-/'+) GPS: {(matchedCandidate.GpsFresh ? "E('41" : "B/JE")}", $"GPS freshness: {(matchedCandidate.GpsFresh ? "live" : "stale")}"),
            L($"نطاق المسافة: {matchedCandidate.DistanceBucket}", $"Distance bucket: {matchedCandidate.DistanceBucket}"),
            L($"المسافة: {matchedCandidate.DistanceKm:0.0} كم", $"Distance: {matchedCandidate.DistanceKm:0.0} km"),
            L($"الطلبات النشطة: {matchedCandidate.ActiveOrders}", $"Active orders: {matchedCandidate.ActiveOrders}"),
            L($"التقييم: {matchedCandidate.Rating:0.0}", $"Rating: {matchedCandidate.Rating:0.0}"),
            matchedCandidate.CommitmentAdjustmentReason switch
            {
                "commitment-score-boost" => L("تأثير الالتزام: تعزيز", "Commitment effect: commitment-score-boost"),
                "rejection-penalty" => L("تأثير الالتزام: خصم", "Commitment effect: rejection-penalty"),
                _ => L("تأثير الالتزام: محايد", "Commitment effect: neutral")
            },
            matchedCandidate.LowConfidenceGps
                ? L("دقة GPS: منخفضة (>100م)", "GPS confidence: low (>100m)")
                : L("دقة GPS: طبيعية", "GPS confidence: normal"),
            matchedCandidate.Verified ? L("التوثيق: معتمد", "Verification: approved") : L("التوثيق: قيد المراجعة", "Verification: pending")
        ];
    }

    private static string BuildPaymentMethodLabel(PaymentMethodType paymentMethod) =>
        paymentMethod switch
        {
            PaymentMethodType.CashOnDelivery => L("الدفع عند الاستلام", "Cash on delivery"),
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

        return string.Join(", ", new[] { address.AddressLine, address.Area, LocalizeCity(address.City) }
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

        return SaudiTime.ToSaudi(estimated).ToString("hh:mm tt", CultureInfo.InvariantCulture);
    }

    private static string BuildPaymentStatusNote(Order order, Payment? payment, IReadOnlyList<Refund>? refunds)
    {
        if (refunds is { Count: > 0 })
        {
            var refund = refunds.OrderByDescending(item => item.CreatedAtUtc).First();
            return L($"استرجاع {refund.Status} بمبلغ {refund.Amount:0.00} ر.س.", $"Refund {refund.Status} for {refund.Amount:0.00} SAR.");
        }

        return order.PaymentStatus switch
        {
            PaymentStatus.Paid => L("الدفع مؤكد ولا توجد محاولة فاشلة.", "Payment confirmed with no active failure trace."),
            PaymentStatus.Failed => L("محاولة الدفع الأخيرة فشلت وتحتاج مراجعة مالية.", "Latest payment attempt failed and needs finance review."),
            PaymentStatus.Pending or PaymentStatus.Initiated => L("الدفع بانتظار التأكيد قبل بدء التنفيذ.", "Payment is pending confirmation before fulfillment moves forward."),
            _ => payment?.ProviderName is not null
                ? L($"عالجنا عبر {payment.ProviderName}.", $"Processed through {payment.ProviderName}.")
                : L("حالة الدفع تحت المراقبة.", "Payment state is being monitored.")
        };
    }

    private static string BuildFulfillmentStatusNote(Order order, DeliveryAssignment? assignment)
    {
        return MapFulfillmentStatus(order.Status, assignment) switch
        {
            "QUEUED" => L("التنفيذ لم يبدأ بعد والطلب لا يزال في الانتظار.", "Execution has not started yet and the order is still queued."),
            "PREPARING" => L("المتجر يقوم بتجهيز الطلب.", "Vendor is actively preparing the order."),
            "READY_FOR_PICKUP" => L("الطلب جاهز وبانتظار الاستلام.", "Order is ready and waiting for pickup."),
            "DRIVER_ASSIGNED" => L("عيّنا مندوب والتوجيه قيد التنفيذ.", "A driver has been assigned and dispatch is in progress."),
            "PICKED_UP" => L("المندوب استلم الطلب ويتجه للتوصيل.", "Driver picked up the order and is moving toward delivery."),
            "ON_ROUTE" => L("المندوب في الطريق إلى العميل.", "Driver is on the way to the customer."),
            "DELIVERED" => L("وصلنا بنجاح.", "Delivery completed successfully."),
            "FAILED" => L("التنفيذ فشل ويحتاج تدخل.", "Fulfillment failed and requires intervention."),
            _ => L("توقف تنفيذ الطلب بعد الإلغاء.", "Order execution stopped after cancellation.")
        };
    }

    private static string BuildSupportSummary(bool isLate, AdminOrderOperationalCaseDto? operationalCase)
    {
        if (operationalCase is not null)
        {
            return L($"حالة {operationalCase.Type.ToLowerInvariant()} مفتوحة ومحولة إلى {operationalCase.QueueLabel}.", $"Open {operationalCase.Type.ToLowerInvariant()} case is routed to {operationalCase.QueueLabel}.");
        }

        return isLate
            ? L("الطلب تجاوز الوقت المتوقع ويحتاج متابعة العمليات.", "Order exceeded the expected SLA and should be monitored by operations.")
            : L("لا توجد حالة دعم نشطة على الطلب.", "No active support case is currently attached to the order.");
    }

    private static string BuildAlertLabel(bool isLate, AdminOrderOperationalCaseDto? operationalCase, string status)
    {
        if (operationalCase is not null)
        {
            return operationalCase.Title;
        }

        if (isLate)
        {
            return L("الطلب متأخر عن المعدل المتوقع", "Order is running behind SLA");
        }

        return status == "CANCELLED"
            ? L("ألغينا الطلب", "Order has been cancelled")
            : L("سير الطلب طبيعي", "Order flow is healthy");
    }

    private static string? BuildCancellationReason(Order order) =>
        order.Status switch
        {
            OrderStatus.Cancelled => L("ملغي من العمليات", "Cancelled by operations"),
            OrderStatus.VendorRejected => L("مرفوض من المتجر", "Rejected by merchant"),
            OrderStatus.DeliveryFailed => L("فشل التوصيل", "Delivery failed"),
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
            order.Notes ?? L("سجّلنا الإلغاء من سير عمل المشرف.", "Cancellation was recorded from the admin workflow."),
            refundType,
            latestRefund is null ? "platform" : "merchant",
            SaudiTime.ToSaudi(order.CancelledAtUtc ?? ResolveLastUpdatedAtUtc(order)).ToString("g", CultureInfo.InvariantCulture),
            L("مكتب العمليات", "Operations desk"),
            L("حدّثنا حالة طلبك إلى ملغي.", "Your order status was updated to cancelled."));
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
                supportCase.Id.ToString(),
                supportCase.Type == OrderSupportCaseType.ReturnRequest ? "REFUND" : ResolveOperationalCaseType(supportCase),
                status,
                supportCase.CustomerVisibleNote ?? supportCase.Message,
                ResolveQueueLabel(supportCase.Queue),
                SaudiTime.ToSaudi(supportCase.CreatedAtUtc).ToString("g", CultureInfo.InvariantCulture),
                SaudiTime.ToSaudi(supportCase.UpdatedAtUtc).ToString("g", CultureInfo.InvariantCulture));
        }

        var latestRefund = refunds?
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        if (latestRefund is null)
        {
            return null;
        }

        return new AdminOrderOperationalCaseDto(
            latestRefund.OrderSupportCaseId?.ToString(),
            "REFUND",
            latestRefund.Status == PaymentStatus.Refunded ? "RESOLVED" : "OPEN",
            latestRefund.Amount >= order.TotalAmount ? L("مراجعة استرجاع كامل", "Full refund review") : L("مراجعة استرجاع جزئي", "Partial refund review"),
            "Finance",
            SaudiTime.ToSaudi(latestRefund.CreatedAtUtc).ToString("g", CultureInfo.InvariantCulture),
            SaudiTime.ToSaudi(latestRefund.UpdatedAtUtc).ToString("g", CultureInfo.InvariantCulture));
    }

    private static IReadOnlyList<AdminOrderTimelineItemDto> BuildAdminTimeline(
        Order order,
        Payment? payment,
        DeliveryAssignment? assignment,
        AdminOrderOperationalCaseDto? operationalCase)
    {
        var steps = new List<AdminOrderTimelineItemDto>();

        // Always add Placed/Created as the first step
        var createdSubtitle = payment?.Status == PaymentStatus.Paid
            ? new LocalizedText("أكدنا الدفع والتحصيل", "Payment captured")
            : new LocalizedText("بانتظار تأكيد الدفع", "Waiting for payment confirmation");

        steps.Add(new AdminOrderTimelineItemDto(
            "أنشأنا الطلب",
            "Order created",
            createdSubtitle.Ar,
            createdSubtitle.En,
            SaudiTime.ToSaudi(order.PlacedAtUtc).ToString("hh:mm tt", CultureInfo.InvariantCulture),
            "COMPLETED",
            order.StatusHistory.Count == 0
        ));

        var historyList = order.StatusHistory
            .OrderBy(history => history.CreatedAtUtc)
            .ToList();

        foreach (var history in historyList)
        {
            var isLast = history == historyList.Last() && operationalCase == null;
            var statusStr = "COMPLETED";
            
            // If it's the last step and the order is not in a terminal state, mark it as IN_PROGRESS
            if (isLast && order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Cancelled && order.Status != OrderStatus.VendorRejected && order.Status != OrderStatus.DeliveryFailed && order.Status != OrderStatus.Refunded)
            {
                statusStr = "IN_PROGRESS";
            }

            var title = OrderStatusLocalization.Localize(history.NewStatus);
            var note = OrderStatusNoteCatalog.LocalizeNote(history.Note);

            steps.Add(new AdminOrderTimelineItemDto(
                title.Ar,
                title.En,
                note.Ar,
                note.En,
                SaudiTime.ToSaudi(history.CreatedAtUtc).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                statusStr,
                isLast
            ));
        }

        // If there's an active operational case, we can append it as a step at the end
        if (operationalCase is not null)
        {
            var caseTitle = OrderStatusNoteCatalog.LocalizeNote(operationalCase.Title);
            var caseSubtitle = OrderStatusNoteCatalog.LocalizeNote(operationalCase.QueueLabel);

            steps.Add(new AdminOrderTimelineItemDto(
                caseTitle.Ar,
                caseTitle.En,
                caseSubtitle.Ar,
                caseSubtitle.En,
                operationalCase.LastUpdatedAt,
                operationalCase.Status == "OPEN" ? "IN_PROGRESS" : "COMPLETED",
                true
            ));
        }

        return steps;
    }

    private static string TranslateOrderStatus(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => L("بانتظار الدفع", "Pending Payment"),
        OrderStatus.Placed => L("مُرسل", "Placed"),
        OrderStatus.PendingVendorAcceptance => L("بانتظار قبول المتجر", "Pending Vendor Acceptance"),
        OrderStatus.Accepted => L("مقبول", "Accepted"),
        OrderStatus.Preparing => L("قيد التجهيز", "Preparing"),
        OrderStatus.ReadyForPickup => L("جاهز للاستلام", "Ready for Pickup"),
        OrderStatus.DriverAssignmentInProgress => L("جاري البحث عن مندوب", "Driver Assignment in Progress"),
        OrderStatus.DriverAssigned => L("عيّنا المندوب", "Driver Assigned"),
        OrderStatus.PickedUp => L("استلمنا", "Picked Up"),
        OrderStatus.OnTheWay => L("في الطريق", "On The Way"),
        OrderStatus.Delivered => L("وصلنا", "Delivered"),
        OrderStatus.Cancelled => L("ملغى", "Cancelled"),
        OrderStatus.VendorRejected => L("مرفوض من المتجر", "Vendor Rejected"),
        OrderStatus.DeliveryFailed => L("فشل التوصيل", "Delivery Failed"),
        OrderStatus.Refunded => L("مسترجع", "Refunded"),
        _ => status.ToString()
    };

    private static string TranslatePaymentStatus(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => L("بانتظار المعالجة", "Pending"),
        PaymentStatus.Paid => L("مدفوع", "Paid"),
        PaymentStatus.Failed => L("فشل", "Failed"),
        PaymentStatus.Refunded => L("مسترجع", "Refunded"),
        PaymentStatus.PartiallyRefunded => L("استرجاع جزئي", "Partially Refunded"),
        PaymentStatus.PendingCollection => L("بانتظار التحصيل", "Pending Collection"),
        _ => status.ToString()
    };

    private static string TranslatePaymentProvider(string? provider) => provider switch
    {
        "CashOnDelivery" => L("الدفع عند الاستلام", "Cash on Delivery"),
        "Wallet" => L("المحفظة", "Wallet"),
        "BankTransfer" => L("تحويل بنكي", "Bank Transfer"),
        "Card" => L("بطاقة ائتمان", "Card"),
        _ => provider ?? string.Empty
    };

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
                L($"الطلب انتقل إلى {TranslateOrderStatus(item.NewStatus)}", $"Order moved to {TranslateOrderStatus(item.NewStatus)}"),
                item.ChangedByUserId.HasValue ? L("مستخدم النظام", "Workflow user") : L("النظام", "System"),
                SaudiTime.ToSaudi(item.CreatedAtUtc).ToString("g", CultureInfo.InvariantCulture),
                "status"))
            .ToList();

        if (payment is not null)
        {
            activities.Insert(0, new AdminOrderActivityDto(
                L($"حالة الدفع: {TranslatePaymentStatus(payment.Status)}", $"Payment state: {TranslatePaymentStatus(payment.Status)}"),
                TranslatePaymentProvider(payment.ProviderName) is var provider && !string.IsNullOrWhiteSpace(provider) ? provider : L("بوابة الدفع", "Payment gateway"),
                SaudiTime.ToSaudi(payment.PaidAtUtc ?? payment.FailedAtUtc ?? payment.CreatedAtUtc).ToString("g", CultureInfo.InvariantCulture),
                "payment"));
        }

        if (assignment?.Driver is not null)
        {
            activities.Insert(0, new AdminOrderActivityDto(
                L($"عيّنا مندوب: {assignment.Driver.User.FullName}", $"Driver assigned: {assignment.Driver.User.FullName}"),
                L("التوجيه", "Dispatch"),
                SaudiTime.ToSaudi(assignment.CreatedAtUtc).ToString("g", CultureInfo.InvariantCulture),
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
                L($"استرجاع {TranslatePaymentStatus(refund.Status)}", $"Refund {TranslatePaymentStatus(refund.Status)}"),
                L("المالية", "Finance"),
                SaudiTime.ToSaudi(refund.CreatedAtUtc).ToString("g", CultureInfo.InvariantCulture),
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
        ReadyForPickup,
        OutForDelivery
    }
}

