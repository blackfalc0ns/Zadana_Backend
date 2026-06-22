using FluentAssertions;
using Moq;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.ResetPassword;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Identity;

public class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IIdentityAccountService> _identityAccountServiceMock = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokenStoreMock = new();
    private readonly Mock<IJwtRevocationStore> _jwtRevocationStoreMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();

    private ResetPasswordCommandHandler CreateHandler() =>
        new(
            _identityAccountServiceMock.Object,
            _refreshTokenStoreMock.Object,
            _jwtRevocationStoreMock.Object,
            _unitOfWorkMock.Object,
            _localizerMock.Object);

    private void SetupLocalizer()
    {
        _localizerMock.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldThrowUnauthorizedException()
    {
        SetupLocalizer();
        _identityAccountServiceMock
            .Setup(s => s.FindByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityAccountSnapshot?)null);
        _identityAccountServiceMock
            .Setup(s => s.CompletePasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetResult(PasswordResetStatus.UserNotFound));

        var handler = CreateHandler();
        var act = () => handler.Handle(new ResetPasswordCommand("unknown@test.com", new string('a', 64), "NewP@ssword1"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_WithInvalidResetToken_ShouldThrowBusinessRuleException()
    {
        SetupLocalizer();
        _identityAccountServiceMock
            .Setup(s => s.FindByIdentifierAsync("test@zadana.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityAccountSnapshot(
                Guid.NewGuid(),
                "Test User",
                "test@zadana.com",
                "01011111111",
                Zadana.Domain.Modules.Identity.Enums.UserRole.Customer,
                1,
                Zadana.Domain.Modules.Identity.Enums.AccountStatus.Active,
                false,
                null,
                null,
                true,
                true,
                false,
                null));
        _identityAccountServiceMock
            .Setup(s => s.CompletePasswordResetAsync("test@zadana.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetResult(PasswordResetStatus.InvalidOrExpiredResetToken));

        var handler = CreateHandler();
        var act = () => handler.Handle(new ResetPasswordCommand("test@zadana.com", new string('a', 64), "NewP@ssword1"), CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "INVALID_RESET_TOKEN");
    }

    [Fact]
    public async Task Handle_WithValidResetToken_ShouldRevokeSessionsAndSave()
    {
        SetupLocalizer();
        var userId = Guid.NewGuid();
        _identityAccountServiceMock
            .Setup(s => s.FindByIdentifierAsync("test@zadana.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityAccountSnapshot(
                userId,
                "Test User",
                "test@zadana.com",
                "01011111111",
                Zadana.Domain.Modules.Identity.Enums.UserRole.Customer,
                1,
                Zadana.Domain.Modules.Identity.Enums.AccountStatus.Active,
                false,
                null,
                null,
                true,
                true,
                false,
                null));
        _identityAccountServiceMock
            .Setup(s => s.CompletePasswordResetAsync("test@zadana.com", It.IsAny<string>(), "NewP@ssword1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetResult(PasswordResetStatus.Succeeded));

        var handler = CreateHandler();
        await handler.Handle(new ResetPasswordCommand("test@zadana.com", new string('a', 64), "NewP@ssword1"), CancellationToken.None);

        _refreshTokenStoreMock.Verify(s => s.RevokeAllByUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _jwtRevocationStoreMock.Verify(s => s.RevokeAllForUserAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
