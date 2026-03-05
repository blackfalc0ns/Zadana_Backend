using FluentAssertions;
using Moq;
using Zadana.Application.Modules.Identity.Commands.RefreshToken;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;

namespace Zadana.Application.Tests.Application.Identity;

/// <summary>
/// Unit tests for RefreshTokenCommandHandler.
/// The handler delegates to IIdentityService.
/// </summary>
public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();

    private RefreshTokenCommandHandler CreateHandler() => new(_identityServiceMock.Object);

    [Fact]
    public async Task Handle_ShouldDelegateToIdentityService()
    {
        // Arrange
        var expectedTokens = new TokenPairDto("new_access", "new_refresh");
        _identityServiceMock
            .Setup(s => s.RefreshTokenAsync("old_refresh_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTokens);

        var command = new RefreshTokenCommand("old_refresh_token");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(expectedTokens);
        result.AccessToken.Should().Be("new_access");
        result.RefreshToken.Should().Be("new_refresh");
        _identityServiceMock.Verify(
            s => s.RefreshTokenAsync("old_refresh_token", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
