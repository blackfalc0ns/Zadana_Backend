using FluentAssertions;
using Moq;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Queries;

public class GetVendorDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithValidId_ReturnsDetailDto()
    {
        var vendorId = Guid.NewGuid();
        var expected = new VendorDetailDto(
            Id: vendorId,
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
            ApprovedByName: null,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow,
            ReviewStartedAtUtc: null,
            ReviewCompletedAtUtc: null,
            RequestedChangesAtUtc: null,
            ReviewDecisionReason: null,
            ReadyForFinalApproval: false,
            OwnerName: "Owner Name",
            OwnerEmail: "owner@test.com",
            OwnerPhone: "123",
            IdNumber: null,
            Nationality: null,
            PayoutCycle: null,
            FinancialLifecycleMode: "weekly",
            OperationsSettings: new VendorOperationsSettingsDto(true, null, null),
            NotificationSettings: new VendorNotificationSettingsDto(true, false, true),
            PrimaryBankAccount: null,
            OperatingHours: [],
            ReviewItems: [],
            RequiredActions: [],
            ReviewDocuments: [],
            ReviewNotes: [],
            BranchesCount: 2,
            BankAccountsCount: 1);

        var readService = new Mock<IVendorReadService>();
        readService
            .Setup(service => service.GetDetailAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetVendorDetailQueryHandler(readService.Object);

        var result = await handler.Handle(new GetVendorDetailQuery(vendorId), default);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Handle_WithInvalidId_ThrowsNotFoundException()
    {
        var vendorId = Guid.NewGuid();
        var readService = new Mock<IVendorReadService>();
        readService
            .Setup(service => service.GetDetailAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorDetailDto?)null);

        var handler = new GetVendorDetailQueryHandler(readService.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetVendorDetailQuery(vendorId), default));
    }
}
