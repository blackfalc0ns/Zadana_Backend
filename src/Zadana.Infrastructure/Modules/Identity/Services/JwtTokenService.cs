using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Modules.Identity.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<TokenPairDto> GenerateTokenPairAsync(IdentityAccountSnapshot user, CancellationToken cancellationToken = default)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("permission_version", user.PermissionVersion.ToString()),
            new Claim(ClaimTypes.Name, user.FullName)
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }
        
        if (!string.IsNullOrEmpty(user.PhoneNumber))
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, user.PhoneNumber));
        }

        var secret = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("JWT Secret is not configured.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryInMinutes = ResolveExpiryMinutes(user.Role);

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryInMinutes),
            signingCredentials: creds
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateSecureRefreshToken();

        return Task.FromResult(new TokenPairDto(accessToken, refreshToken));
    }

    private double ResolveExpiryMinutes(UserRole role) =>
        role switch
        {
            UserRole.Admin or UserRole.SuperAdmin =>
                ReadMinutes("JwtSettings:AdminExpiryMinutes", ReadMinutes("JwtSettings:ExpiryMinutes", 60)),
            UserRole.Vendor or UserRole.VendorStaff =>
                ReadMinutes("JwtSettings:VendorExpiryMinutes", ReadMinutes("JwtSettings:ExpiryMinutes", 60)),
            _ => ReadMinutes("JwtSettings:ExpiryMinutes", 60)
        };

    private double ReadMinutes(string key, double fallback)
    {
        var raw = _configuration[key];
        return double.TryParse(raw, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static string GenerateSecureRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
