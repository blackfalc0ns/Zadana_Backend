namespace Zadana.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? GuestDeviceId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    string? GetDeviceInfo();
}
