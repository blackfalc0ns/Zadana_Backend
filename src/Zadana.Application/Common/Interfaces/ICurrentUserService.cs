namespace Zadana.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? GuestDeviceId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    string? GetDeviceInfo();

    /// <summary>JTI claim on the bearer token that authenticated this request.</summary>
    string? AccessTokenJti { get; }

    /// <summary>UTC expiry of the bearer token (exp claim) when available.</summary>
    DateTime? AccessTokenExpiresAtUtc { get; }
}
