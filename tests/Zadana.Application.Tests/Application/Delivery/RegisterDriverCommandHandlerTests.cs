using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Tests.Application.Delivery;

/// <summary>
/// Unit tests for RegisterDriverCommandHandler.
/// </summary>
public class RegisterDriverCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private RegisterDriverCommandHandler CreateHandler() =>
        new(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _jwtTokenServiceMock.Object,
            _dbContextMock.Object
        );

    private RegisterDriverCommand CreateValidCommand() =>
        new(
            "Ahmed Driver", "driver@test.com", "01099999999", "P@ssword1",
            "Car", "12345678901234", "DL-12345", "Cairo, Egypt",
            "https://id.png", "https://license.png", "https://vehicle.png", "https://photo.png"
        );

    // ─── Duplicate User ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var existingUser = new User("Existing", "driver@test.com", "01099999999", "hash",
            Zadana.Domain.Modules.Identity.Enums.UserRole.Driver);

        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync("driver@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var command = CreateValidCommand();
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "USER_ALREADY_EXISTS");
    }

    // ─── Successful Registration ───────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidData_ShouldCreateUserDriverAndReturnAuth()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _passwordHasherMock.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed_pw");

        var tokens = new TokenPairDto("access_token", "refresh_token");
        _jwtTokenServiceMock
            .Setup(j => j.GenerateTokenPairAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        var mockDriverSet = new Mock<DbSet<Driver>>();
        var mockRefreshTokenSet = new Mock<DbSet<RefreshToken>>();
        _dbContextMock.Setup(c => c.Drivers).Returns(mockDriverSet.Object);
        _dbContextMock.Setup(c => c.RefreshTokens).Returns(mockRefreshTokenSet.Object);

        var command = CreateValidCommand();
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Email.Should().Be("driver@test.com");
        result.Tokens.AccessToken.Should().Be("access_token");

        _userRepositoryMock.Verify(r => r.Add(It.IsAny<User>()), Times.Once);
        mockDriverSet.Verify(d => d.Add(It.IsAny<Driver>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
