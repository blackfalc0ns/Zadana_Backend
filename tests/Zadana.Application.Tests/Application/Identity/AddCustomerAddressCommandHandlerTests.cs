using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Commands.AddCustomerAddress;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Identity;

/// <summary>
/// Unit tests for AddCustomerAddressCommandHandler.
/// We mock IApplicationDbContext to avoid hitting the real database.
/// </summary>
public class AddCustomerAddressCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private AddCustomerAddressCommandHandler CreateHandler() =>
        new(_dbContextMock.Object);

    // ─── User Not Found ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldThrowNotFoundException()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Setup an empty Users DbSet
        var emptyUsers = Array.Empty<User>().AsQueryable();
        var mockUserSet = new Mock<DbSet<User>>();
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(emptyUsers.Provider);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(emptyUsers.Expression);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(emptyUsers.ElementType);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(emptyUsers.GetEnumerator());

        _dbContextMock.Setup(c => c.Users).Returns(mockUserSet.Object);

        var command = new AddCustomerAddressCommand(
            nonExistentUserId, "Test Name", "01011111111", "Street 1",
            null, null, null, null, null, null, null, null);

        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ─── Successful Add ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidData_ShouldAddAddressAndReturnGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = new User("Test User", "test@zadana.com", "01011111111", "hash", UserRole.Customer);

        var userList = new List<User> { existingUser }.AsQueryable();
        var mockUserSet = new Mock<DbSet<User>>();
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(userList.Provider);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(userList.Expression);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(userList.ElementType);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(userList.GetEnumerator());

        // Use AddressId from userId workaround — we override userId in command to match
        var userWithId = typeof(User).GetProperty("Id")!;
        // Use existingUser.Id for the command
        var existingUserId = existingUser.Id;

        _dbContextMock.Setup(c => c.Users).Returns(mockUserSet.Object);

        var mockAddressSet = new Mock<DbSet<CustomerAddress>>();
        _dbContextMock.Setup(c => c.CustomerAddresses).Returns(mockAddressSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new AddCustomerAddressCommand(
            existingUserId, "Ahmed Ali", "01011122233", "Street 15",
            "Home", "42", "3", "10", "Cairo", "Nasr City", 30.044m, 31.235m);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty("A valid Guid should be returned for the new Address");
        mockAddressSet.Verify(d => d.Add(It.IsAny<CustomerAddress>()), Times.Once);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Label Parsing ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("Home", AddressLabel.Home)]
    [InlineData("Work", AddressLabel.Work)]
    [InlineData("Other", AddressLabel.Other)]
    public async Task Handle_WithValidLabelString_ShouldParseEnumCorrectly(
        string labelString, AddressLabel expectedLabel)
    {
        // Arrange
        var existingUser = new User("Test User", "t@t.com", "01022222222", "hash", UserRole.Customer);

        var userList = new List<User> { existingUser }.AsQueryable();
        var mockUserSet = new Mock<DbSet<User>>();
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(userList.Provider);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(userList.Expression);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(userList.ElementType);
        mockUserSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(userList.GetEnumerator());

        CustomerAddress? capturedAddress = null;
        var mockAddressSet = new Mock<DbSet<CustomerAddress>>();
        mockAddressSet.Setup(s => s.Add(It.IsAny<CustomerAddress>()))
            .Callback<CustomerAddress>(a => capturedAddress = a);

        _dbContextMock.Setup(c => c.Users).Returns(mockUserSet.Object);
        _dbContextMock.Setup(c => c.CustomerAddresses).Returns(mockAddressSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new AddCustomerAddressCommand(
            existingUser.Id, "Test Name", "01011111111", "Main St",
            labelString, null, null, null, null, null, null, null);

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        capturedAddress.Should().NotBeNull();
        capturedAddress!.Label.Should().Be(expectedLabel,
            $"Label string '{labelString}' should be parsed to {expectedLabel}");
    }
}
