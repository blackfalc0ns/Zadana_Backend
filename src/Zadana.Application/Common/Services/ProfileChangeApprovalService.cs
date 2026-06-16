using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Common.Services;

public sealed class ProfileChangeApprovalService : IProfileChangeApprovalService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _context;
    private readonly IAdminAlertService _adminAlertService;

    public ProfileChangeApprovalService(
        IApplicationDbContext context,
        IAdminAlertService adminAlertService)
    {
        _context = context;
        _adminAlertService = adminAlertService;
    }

    public async Task<Guid> SubmitAsync(
        Guid requestedByUserId,
        Guid targetUserId,
        string action,
        string summary,
        object payload,
        ProfileChangeApprovalAlert alert,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));

        var existing = await _context.AccessApprovalRequests
            .Where(request =>
                request.RequestedByUserId == requestedByUserId &&
                request.TargetUserId == targetUserId &&
                request.Action == action &&
                request.PayloadHash == payloadHash &&
                request.Status == AccessApprovalStatus.Pending)
            .OrderByDescending(request => request.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            await SendAlertAsync(alert, existing.Id, action, cancellationToken);
            return existing.Id;
        }

        var approval = new AccessApprovalRequest(
            requestedByUserId,
            targetUserId,
            action,
            summary,
            payloadHash,
            payloadJson);

        _context.AccessApprovalRequests.Add(approval);
        await _context.SaveChangesAsync(cancellationToken);

        await SendAlertAsync(alert, approval.Id, action, cancellationToken);
        return approval.Id;
    }

    private Task SendAlertAsync(
        ProfileChangeApprovalAlert alert,
        Guid approvalRequestId,
        string action,
        CancellationToken cancellationToken)
    {
        return _adminAlertService.SendAsync(
            new AdminAlertRequest(
                alert.Type,
                alert.Category,
                alert.Priority,
                alert.TitleAr,
                alert.TitleEn,
                alert.BodyAr,
                alert.BodyEn,
                alert.ReferenceId,
                alert.TargetUrl,
                new
                {
                    approvalRequestId,
                    action,
                    payload = alert.Data
                }),
            cancellationToken);
    }
}
