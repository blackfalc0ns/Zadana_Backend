using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Commands.RegisterUser;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Tests.Application.Identity;

/// <summary>
/// Unit tests for RegisterUserCommandHandler (admin-created users).
/// </summary>
public class RegisterUserCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();

    private RegisterUserCommandHandler CreateHandler() =>
        new(_dbContextMock.Object, _passwordHasherMock.Object);

    private void SetupEmptyUsers()
    {
        var emptyUsers = Array.Empty<User>().AsQueryable();
        var mockUserSet = new Mock<DbSet<User>>();
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(emptyUsers.Provider);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(emptyUsers.Expression);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(emptyUsers.ElementType);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(emptyUsers.GetEnumerator());
        _dbContextMock.Setup(c => c.Users).Returns(mockUserSet.Object);
    }

    private void SetupUsersWithEmail(string existingEmail)
    {
        var user = new User("Existing", existingEmail, "01000000000", "hash",
            Zadana.Domain.Modules.Identity.Enums.UserRole.Customer);
        var userList = new List<User> { user }.AsQueryable();
        var mockUserSet = new Mock<DbSet<User>>();
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(userList.Provider);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(userList.Expression);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(userList.ElementType);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(userList.GetEnumerator());
        _dbContextMock.Setup(c => c.Users).Returns(mockUserSet.Object);
    }

    // ─── Duplicate Email ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenEmailExists_ShouldThrowBusinessRuleException()
    {
        // Arrange
        SetupUsersWithEmail("taken@test.com");

        var command = new RegisterUserCommand("New User", "taken@test.com", "01011111111", "P@ssword1", "Customer");
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "User.EmailConflict");
    }

    // ─── Invalid Role ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenInvalidRole_ShouldThrowBusinessRuleException()
    {
        // Arrange
        SetupEmptyUsers();

        var command = new RegisterUserCommand("New User", "new@test.com", "01011111111", "P@ssword1", "InvalidRole");
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "User.InvalidRole");
    }

    // ─── Success ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidData_ShouldAddUserAndReturnGuid()
    {
        // Arrange
        SetupEmptyUsers();
        _passwordHasherMock.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed_pw");
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new RegisterUserCommand("Ahmed Ali", "ahmed@test.com", "01011111111", "P@ssword1", "Admin");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty("A valid Guid should be returned for the new User");
        _passwordHasherMock.Verify(p => p.HashPassword("P@ssword1"), Times.Once);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
