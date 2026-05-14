using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Queries;

public class GetVendorProfileQueryHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsWorkspaceDto()
    {
        var userId = Guid.NewGuid();
        var expected = CreateWorkspaceDto();

        _currentUserMock.Setup(currentUser => currentUser.UserId).Returns(userId);

        var readService = new Mock<IVendorReadService>();
        readService
            .Setup(service => service.GetWorkspaceByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetVendorProfileQueryHandler(readService.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetVendorProfileQuery(), default);

        result.Should().BeEquivalentTo(expected);
    }

    private static VendorWorkspaceDto CreateWorkspaceDto() =>
        new(
            Id: Guid.NewGuid(),
            BusinessNameAr: "Business Ar",
            BusinessNameEn: "Business En",
            BusinessType: "Retail",
            CommercialRegistrationNumber: "CR001",
            CommercialRegistrationExpiryDate: null,
            TaxId: null,
            LicenseNumber: null,
            ContactEmail: "contact@test.com",
            ContactPhone: "999",
            DescriptionAr: null,
            DescriptionEn: null,
            Region: null,
            City: null,
            NationalAddress: null,
            PrimaryBranchLatitude: null,
            PrimaryBranchLongitude: null,
            OwnerName: "Owner Name",
            OwnerEmail: "owner@test.com",
            OwnerPhone: "1234567890",
            IdNumber: null,
            Nationality: null,
            PayoutCycle: "weekly",
            FinancialLifecycleMode: "manual",
            CommissionRate: 10m,
            Status: "Active",
            AccountStatus: "Active",
            IsLoginLocked: false,
            LockedAtUtc: null,
            ArchivedAtUtc: null,
            SuspendedAtUtc: null,
            RejectionReason: null,
            SuspensionReason: null,
            LockReason: null,
            ArchiveReason: null,
            LogoUrl: null,
            CommercialRegisterDocumentUrl: null,
            TaxDocumentUrl: null,
            LicenseDocumentUrl: null,
            ApprovedAtUtc: null,
            ApprovedByName: "Admin User",
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow,
            OperationsSettings: new VendorOperationsSettingsDto(true, null, null),
            NotificationSettings: new VendorNotificationSettingsDto(true, false, true),
            BranchesCount: 1,
            BankAccountsCount: 1,
            PrimaryBankAccount: null,
            OperatingHours: [],
            ReviewState: "UnderReview",
            CommercialAccessEnabled: false,
            AssignedReviewerId: null,
            AssignedReviewerName: null,
            ReviewSubmittedAtUtc: null,
            ReviewStartedAtUtc: null,
            ReviewCompletedAtUtc: null,
            RequestedChangesAtUtc: null,
            LastReviewDecision: null,
            ReviewSummary: new VendorWorkspaceReviewSummaryDto(0, 0, 0, 0, 0, 0),
            ReviewItems: [],
            RequiredActions: [],
            ReviewAuditEntries: [],
            MissingDocumentsCount: 0,
            CanSubmitForReview: false);

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_ThrowsUnauthorizedException()
    {
        _currentUserMock.Setup(currentUser => currentUser.UserId).Returns((Guid?)null);

        var readService = new Mock<IVendorReadService>();
        var handler = new GetVendorProfileQueryHandler(readService.Object, _currentUserMock.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new GetVendorProfileQuery(), default));
    }

    [Fact]
    public async Task Handle_WhenUserNotVendor_ThrowsNotFoundException()
    {
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(currentUser => currentUser.UserId).Returns(userId);

        var readService = new Mock<IVendorReadService>();
        readService
            .Setup(service => service.GetWorkspaceByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorWorkspaceDto?)null);

        var handler = new GetVendorProfileQueryHandler(readService.Object, _currentUserMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetVendorProfileQuery(), default));
    }
}
