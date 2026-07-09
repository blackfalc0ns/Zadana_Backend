using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.ReopenVendorReview;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class ReopenVendorReviewCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IVendorCommunicationService> _vendorCommunicationServiceMock = new();
    private readonly Mock<IVendorReadService> _vendorReadServiceMock = new();
    private readonly Mock<IVendorRepository> _vendorRepositoryMock = new();
    private readonly Mock<IVendorReviewAuditService> _vendorReviewAuditServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task Handle_WithRejectedVendor_ReopensReview()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "vendor@test.com", "123");
        vendor.Reject("Missing documents");
        _currentUserServiceMock.Setup(service => service.UserId).Returns(Guid.NewGuid());
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _vendorReadServiceMock
            .Setup(service => service.GetDetailAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetail(vendor));

        var handler = CreateHandler();

        var result = await handler.Handle(new ReopenVendorReviewCommand(vendor.Id), default);

        vendor.Status.Should().Be(VendorStatus.PendingReview);
        vendor.RejectionReason.Should().BeNull();
        result.Id.Should().Be(vendor.Id);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenVendorIsNotRejected_ThrowsBusinessRuleException()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "vendor@test.com", "123");
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new ReopenVendorReviewCommand(vendor.Id), default));

        vendor.Status.Should().Be(VendorStatus.PendingReview);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private ReopenVendorReviewCommandHandler CreateHandler() =>
        new(
            _vendorRepositoryMock.Object,
            _vendorReadServiceMock.Object,
            _vendorReviewAuditServiceMock.Object,
            _vendorCommunicationServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

    private static VendorDetailDto CreateDetail(Vendor vendor) =>
        new(
            Id: vendor.Id,
            BusinessNameAr: vendor.BusinessNameAr,
            BusinessNameEn: vendor.BusinessNameEn,
            BusinessType: vendor.BusinessType,
            CommercialRegistrationNumber: vendor.CommercialRegistrationNumber,
            CommercialRegistrationExpiryDate: vendor.CommercialRegistrationExpiryDate,
            TaxId: vendor.TaxId,
            LicenseNumber: vendor.LicenseNumber,
            ContactEmail: vendor.ContactEmail,
            ContactPhone: vendor.ContactPhone,
            DescriptionAr: vendor.DescriptionAr,
            DescriptionEn: vendor.DescriptionEn,
            Region: vendor.Region,
            City: vendor.City,
            NationalAddress: vendor.NationalAddress,
            PrimaryBranchLatitude: null,
            PrimaryBranchLongitude: null,
            CommissionRate: vendor.CommissionRate,
            Status: vendor.Status.ToString(),
            AccountStatus: vendor.Status == VendorStatus.Active ? "Active" : "Pending",
            IsLoginLocked: vendor.LockedAtUtc.HasValue,
            LockedAtUtc: vendor.LockedAtUtc,
            ArchivedAtUtc: vendor.ArchivedAtUtc,
            SuspendedAtUtc: vendor.SuspendedAtUtc,
            RejectionReason: vendor.RejectionReason,
            SuspensionReason: vendor.SuspensionReason,
            LockReason: vendor.LockReason,
            ArchiveReason: vendor.ArchiveReason,
            LogoUrl: vendor.LogoUrl,
            CommercialRegisterDocumentUrl: vendor.CommercialRegisterDocumentUrl,
            TaxDocumentUrl: vendor.TaxDocumentUrl,
            LicenseDocumentUrl: vendor.LicenseDocumentUrl,
            ApprovedAtUtc: vendor.ApprovedAtUtc,
            ApprovedByName: null,
            CreatedAtUtc: vendor.CreatedAtUtc,
            UpdatedAtUtc: vendor.UpdatedAtUtc,
            ReviewStartedAtUtc: null,
            ReviewCompletedAtUtc: null,
            RequestedChangesAtUtc: null,
            ReviewDecisionReason: null,
            ReadyForFinalApproval: false,
            OwnerName: vendor.OwnerName ?? string.Empty,
            OwnerEmail: vendor.OwnerEmail ?? string.Empty,
            OwnerPhone: vendor.OwnerPhone ?? string.Empty,
            IdNumber: vendor.IdNumber,
            Nationality: vendor.Nationality,
            PayoutCycle: vendor.PayoutCycle,
            FinancialLifecycleMode: vendor.FinancialLifecycleMode.ToString(),
            OperationsSettings: new VendorOperationsSettingsDto(vendor.AcceptOrders, vendor.MinimumOrderAmount, vendor.PreparationTimeMinutes),
            NotificationSettings: new VendorNotificationSettingsDto(
                vendor.EmailNotificationsEnabled,
                vendor.SmsNotificationsEnabled,
                vendor.NewOrdersNotificationsEnabled,
                vendor.NotificationSound),
            PrimaryBankAccount: null,
            OperatingHours: [],
            ReviewItems: [],
            RequiredActions: [],
            ReviewDocuments: [],
            ReviewNotes: [],
            RiskIndicators: [],
            BranchesCount: 0,
            BankAccountsCount: 0);
}
