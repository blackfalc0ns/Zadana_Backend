namespace Zadana.Application.Modules.Identity.Interfaces;

public record GoogleIdTokenProfile(
    string Subject,
    string Email,
    bool EmailVerified,
    string FullName,
    string? GivenName,
    string? FamilyName,
    string? PictureUrl);

public interface IGoogleIdTokenVerifier
{
    Task<GoogleIdTokenProfile> VerifyAsync(string idToken, CancellationToken cancellationToken = default);
}
