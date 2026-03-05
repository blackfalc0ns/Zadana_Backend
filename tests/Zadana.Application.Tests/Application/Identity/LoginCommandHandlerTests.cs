using FluentAssertions;
using Moq;
using Zadana.Application.Modules.Identity.Commands.Login;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Tests.Application.Identity;

/// <summary>
/// Unit tests for LoginCommandHandler.
/// The handler delegates to IIdentityService, so we verify delegation.
/// </summary>
public class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();

    private LoginCommandHandler CreateHandler() => new(_identityServiceMock.Object);

    [Fact]
    public async Task Handle_ShouldDelegateToIdentityService()
    {
        // Arrange
        var expectedResponse = new AuthResponseDto(
            new TokenPairDto("access_token", "refresh_token"),
            new CurrentUserDto(Guid.NewGuid(), "Ahmed", "ahmed@test.com", "01011111111", "Customer"));

        _identityServiceMock
            .Setup(s => s.LoginAsync("ahmed@test.com", "P@ssword1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var command = new LoginCommand("ahmed@test.com", "P@ssword1");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResponse);
        _identityServiceMock.Verify(
            s => s.LoginAsync("ahmed@test.com", "P@ssword1", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithExpectedRoles_ShouldPassRolesToService()
    {
        // Arrange
        var expectedResponse = new AuthResponseDto(
            new TokenPairDto("access", "refresh"),
            new CurrentUserDto(Guid.NewGuid(), "Admin", "admin@test.com", "010", "Admin"));

        var roles = new[] { UserRole.Admin };
        _identityServiceMock
            .Setup(s => s.LoginAsync("admin@test.com", "P@ssword1", roles, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var command = new LoginCommand("admin@test.com", "P@ssword1", roles);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResponse);
        _identityServiceMock.Verify(
            s => s.LoginAsync("admin@test.com", "P@ssword1", roles, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
