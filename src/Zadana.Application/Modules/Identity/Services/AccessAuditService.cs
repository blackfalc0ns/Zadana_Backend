using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Application.Modules.Identity.Services;

public interface IAccessAuditService
{
    void Add(Guid targetUserId, string action, string summary, object? before = null, object? after = null);
}

public sealed class AccessAuditService : IAccessAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AccessAuditService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public void Add(Guid targetUserId, string action, string summary, object? before = null, object? after = null)
    {
        var deviceInfo = _currentUserService.GetDeviceInfo();
        var (ip, userAgent) = ParseDeviceInfo(deviceInfo);

        _context.AccessAuditLogs.Add(new AccessAuditLog(
            _currentUserService.UserId,
            targetUserId,
            action,
            summary,
            Serialize(before),
            Serialize(after),
            ip,
            userAgent));
    }

    private static string? Serialize(object? value) =>
        value is null
            ? null
            : JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static (string? Ip, string? UserAgent) ParseDeviceInfo(string? deviceInfo)
    {
        if (string.IsNullOrWhiteSpace(deviceInfo))
        {
            return (null, null);
        }

        const string separator = " | Device: ";
        var parts = deviceInfo.Split(separator, 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return (null, deviceInfo);
        }

        return (parts[0].Replace("IP: ", string.Empty).Trim(), parts[1].Trim());
    }
}
