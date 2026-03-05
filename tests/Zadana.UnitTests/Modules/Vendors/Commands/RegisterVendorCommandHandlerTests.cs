using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.RegisterVendor;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class RegisterVendorCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<IJwtTokenService> _jwtMock = new();

    private RegisterVendorCommand CreateValidCommand() => new(
        "John Doe", "john@test.com", "1234567890", "password123",
        "Business Ar", "Business En", "Retail", "CR123",
        "contact@test.com", "0987654321", null, null, null,
        "Branch 1", "Address 1", 0m, 0m, "1111111111", 5m);

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_ThrowsBusinessRuleException()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var existingUser = new User("Existing", "exist@test.com", "123", "hash", UserRole.Customer);

        _userRepoMock.Setup(x => x.GetByIdentifierAsync(It.IsAny<string>(), default))
            .ReturnsAsync(existingUser);

        var handler = new RegisterVendorCommandHandler(
            _userRepoMock.Object, _unitOfWorkMock.Object, _hasherMock.Object,
            _jwtMock.Object, db);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(CreateValidCommand(), default));
        ex.ErrorCode.Should().Be("USER_ALREADY_EXISTS");
    }
}
