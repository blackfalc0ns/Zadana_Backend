namespace Zadana.Application.Modules.Identity.DTOs;

public record TokenPairDto(string AccessToken, string RefreshToken);

public record CurrentUserDto(Guid Id, string FullName, string? Email, string? Phone, string Role);

public record AuthResponseDto(TokenPairDto Tokens, CurrentUserDto User);
