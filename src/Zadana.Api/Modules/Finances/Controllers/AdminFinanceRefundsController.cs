using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Authorization;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/finances/refunds")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminFinanceRefundsController(
    IApplicationDbContext context) : ApiControllerBase
{
    [HttpGet]
    [RequireAccess(PermissionKeys.Admin.FinancesView)]
    public async Task<ActionResult<AdminFinanceRefundCaseListDto>> GetRefundCases(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] Guid? vendorId = null,
        [FromQuery] Guid? driverId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = context.OrderSupportCases
            .AsNoTracking()
            .Where(item => item.Type == OrderSupportCaseType.ReturnRequest)
            .Include(item => item.Order)
                .ThenInclude(order => order!.Vendor)
            .AsQueryable();

        if (Enum.TryParse<OrderSupportCaseStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(item => item.Status == parsedStatus);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(item => item.OrderId.HasValue && item.Order!.VendorId == vendorId.Value);
        }

        if (driverId.HasValue)
        {
            query = query.Where(item => item.DriverId == driverId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var cases = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.OrderId,
                OrderRef = item.Order != null ? item.Order.OrderNumber : null,
                VendorId = item.Order != null ? item.Order.VendorId : (Guid?)null,
                VendorName = item.Order != null
                    ? (item.Order.Vendor.BusinessNameAr ?? item.Order.Vendor.BusinessNameEn)
                    : null,
                item.DriverId,
                item.RequestedRefundAmount,
                item.ApprovedRefundAmount,
                Status = item.Status.ToString(),
                item.CreatedAtUtc,
                Reason = item.ReasonCode ?? item.Message
            })
            .ToListAsync(cancellationToken);

        var caseIds = cases.Select(item => item.Id).ToList();
        var refundTotalsByCaseId = caseIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await context.Refunds
                .AsNoTracking()
                .Where(refund => refund.OrderSupportCaseId.HasValue && caseIds.Contains(refund.OrderSupportCaseId.Value))
                .GroupBy(refund => refund.OrderSupportCaseId!.Value)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group.Sum(refund => refund.ApprovedAmount),
                    cancellationToken);

        var missingDriverIds = cases
            .Where(item => !item.DriverId.HasValue && item.OrderId.HasValue)
            .Select(item => item.OrderId!.Value)
            .ToList();
        var latestDriverByOrder = missingDriverIds.Count == 0
            ? new Dictionary<Guid, Guid?>()
            : await context.DeliveryAssignments
                .AsNoTracking()
                .Where(assignment => missingDriverIds.Contains(assignment.OrderId) && assignment.DriverId.HasValue)
                .GroupBy(assignment => assignment.OrderId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group
                        .OrderByDescending(assignment => assignment.CreatedAtUtc)
                        .Select(assignment => assignment.DriverId)
                        .FirstOrDefault(),
                    cancellationToken);

        var resolvedDriverIds = cases
            .Select(item => item.DriverId ?? (item.OrderId.HasValue && latestDriverByOrder.TryGetValue(item.OrderId.Value, out var latest) ? latest : null))
            .Where(driver => driver.HasValue)
            .Select(driver => driver!.Value)
            .Distinct()
            .ToList();
        var driversById = resolvedDriverIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Drivers
                .AsNoTracking()
                .Include(driver => driver.User)
                .Where(driver => resolvedDriverIds.Contains(driver.Id))
                .ToDictionaryAsync(driver => driver.Id, driver => driver.User.FullName, cancellationToken);

        var items = cases.Select(item =>
        {
            var resolvedDriverId = item.DriverId
                ?? (item.OrderId.HasValue && latestDriverByOrder.TryGetValue(item.OrderId.Value, out var latest) ? latest : null);
            var approvedAmount = item.ApprovedRefundAmount
                ?? refundTotalsByCaseId.GetValueOrDefault(item.Id, 0m);
            return new AdminFinanceRefundCaseDto(
                item.Id,
                item.OrderId,
                item.OrderRef,
                item.VendorId,
                item.VendorName ?? "Unknown vendor",
                resolvedDriverId,
                resolvedDriverId.HasValue ? driversById.GetValueOrDefault(resolvedDriverId.Value) : null,
                item.RequestedRefundAmount ?? 0m,
                approvedAmount,
                item.Status,
                item.CreatedAtUtc,
                item.Reason);
        }).ToList();

        return Ok(new AdminFinanceRefundCaseListDto(items, page, pageSize, totalCount));
    }
}

public sealed record AdminFinanceRefundCaseListDto(
    IReadOnlyList<AdminFinanceRefundCaseDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminFinanceRefundCaseDto(
    Guid Id,
    Guid? OrderId,
    string? OrderRef,
    Guid? VendorId,
    string VendorName,
    Guid? DriverId,
    string? DriverName,
    decimal RequestedAmount,
    decimal ApprovedAmount,
    string Status,
    DateTime CreatedAt,
    string? Reason);
