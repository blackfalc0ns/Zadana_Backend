using Zadana.Application.Common.Interfaces;

namespace Zadana.UnitTests.Common;

internal sealed class FakeCurrentUserService : ICurrentUserService
{
    public FakeCurrentUserService(
        Guid? userId = null,
        bool isAuthenticated = false,
        string? guestDeviceId = null,
        string? role = null,
        string? deviceInfo = null)
    {
        UserId = userId;
        IsAuthenticated = isAuthenticated;
        GuestDeviceId = guestDeviceId;
        Role = role;
        _deviceInfo = deviceInfo;
    }

    private readonly string? _deviceInfo;

    public Guid? UserId { get; }
    public string? GuestDeviceId { get; }
    public string? Role { get; }
    public bool IsAuthenticated { get; }

    public string? GetDeviceInfo() => _deviceInfo;
}
