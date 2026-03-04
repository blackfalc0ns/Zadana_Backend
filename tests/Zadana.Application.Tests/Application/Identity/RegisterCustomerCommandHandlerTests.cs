using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Commands.RegisterCustomer;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Identity;

/// <summary>
/// Unit tests for RegisterCustomerCommandHandler.
/// Dependencies are mocked so tests are fast and isolated.
/// </summary>
public class RegisterCustomerCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IOtpService> _otpServiceMock = new();
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private RegisterCustomerCommandHandler CreateHandler() =>
        new(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _jwtTokenServiceMock.Object,
            _otpServiceMock.Object,
            _dbContextMock.Object
        );

    private void SetupTokenService()
    {
        var tokens = new TokenPairDto("access_token_xyz", "refresh_token_abc");
        _jwtTokenServiceMock
            .Setup(x => x.GenerateTokenPairAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);
    }

    // ─── Duplicate User Tests ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var existingUser = new User("Existing User", "taken@mail.com", "01099999999", "hash", Zadana.Domain.Modules.Identity.Enums.UserRole.Customer);
        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync("taken@mail.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var command = new RegisterCustomerCommand(
            "New User", "taken@mail.com", "01011111111", "P@ssword1", null, "Address Line", "Home", "123", "1", "1A", "City", "Area", 30.0m, 31.0m);

        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "USER_ALREADY_EXISTS",
                "the exception should carry the correct business error code");
    }

    // ─── Successful Registration Tests ─────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidData_ShouldAddUserToRepository()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _passwordHasherMock
            .Setup(p => p.HashPassword(It.IsAny<string>()))
            .Returns("hashed_password");

        SetupTokenService();

        var command = new RegisterCustomerCommand(
            "Ahmed Ali", "ahmed@test.com", "01011122233", "P@ssword1", null, "Address Line", "Home", "123", "1", "1A", "City", "Area", 30.0m, 31.0m);
        
        var addresses = new Mock<Microsoft.EntityFrameworkCore.DbSet<CustomerAddress>>();
        _dbContextMock.Setup(x => x.CustomerAddresses).Returns(addresses.Object);

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(r => r.Add(It.IsAny<User>()), Times.Once,
            "Repository.Add should be called exactly once");
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldSendOtpSms()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _passwordHasherMock
            .Setup(p => p.HashPassword(It.IsAny<string>()))
            .Returns("hashed_password");

        SetupTokenService();

        var command = new RegisterCustomerCommand(
            "Sara Salem", "sara@test.com", "01099988877", "P@ssword1", null, "Address Line", "Home", "123", "1", "1A", "City", "Area", 30.0m, 31.0m);

        var addresses = new Mock<Microsoft.EntityFrameworkCore.DbSet<CustomerAddress>>();
        _dbContextMock.Setup(x => x.CustomerAddresses).Returns(addresses.Object);

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _otpServiceMock.Verify(
            o => o.SendOtpSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "OTP SMS should be sent exactly once upon registration");
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldReturnAuthResponseWithUser()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _passwordHasherMock
            .Setup(p => p.HashPassword(It.IsAny<string>()))
            .Returns("hashed_pw");

        SetupTokenService();

        var command = new RegisterCustomerCommand(
            "Omar Tarek", "omar@test.com", "01011112222", "P@ssword1", null, "Address Line", "Home", "123", "1", "1A", "City", "Area", 30.0m, 31.0m);

        var addresses = new Mock<Microsoft.EntityFrameworkCore.DbSet<CustomerAddress>>();
        _dbContextMock.Setup(x => x.CustomerAddresses).Returns(addresses.Object);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Email.Should().Be("omar@test.com");
        result.Tokens.Should().NotBeNull();
        result.Tokens.AccessToken.Should().Be("access_token_xyz");
    }
}
