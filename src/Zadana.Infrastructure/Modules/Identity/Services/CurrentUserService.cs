using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Modules.Identity.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                       
            if (idClaim != null && Guid.TryParse(idClaim, out var guid))
            {
                return guid;
            }
            
            return null;
        }
    }

    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string? GetDeviceInfo()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var userAgent = request?.Headers["User-Agent"].ToString();
        var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        
        if (string.IsNullOrEmpty(userAgent) && string.IsNullOrEmpty(ipAddress)) 
            return "Unknown Device";
        
        return $"IP: {ipAddress} | Device: {userAgent}";
    }
}
