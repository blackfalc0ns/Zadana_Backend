using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.RequestVendorDocuments;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class RequestVendorDocumentsCommandHandlerTests
{
    private readonly Mock<IVendorRepository> _vendorRepositoryMock = new();
    private readonly Mock<IVendorReviewAuditService> _vendorReviewAuditServiceMock = new();
    private readonly Mock<IVendorCommunicationService> _vendorCommunicationServiceMock = new();
    private readonly Mock<IVendorReadService> _vendorReadServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task Handle_WithSpecificUploadedDocument_MarksOnlyThatDocumentForReupload()
    {
        var vendor = new Vendor(
            Guid.NewGuid(),
            "Ar",
            "En",
            "Retail",
            "CR",
            "vendor@test.com",
            "123",
            taxDocumentUrl: "https://files.test/tax.pdf");
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _vendorReadServiceMock
            .Setup(service => service.GetDetailAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetail(vendor));

        var handler = CreateHandler();

        await handler.Handle(
            new RequestVendorDocumentsCommand(vendor.Id, "tax", "Upload a current tax certificate."),
            default);

        vendor.DocumentReviews.Should().ContainSingle();
        var review = vendor.DocumentReviews.Single();
        review.Type.Should().Be(VendorDocumentType.Tax);
        review.Decision.Should().Be(VendorDocumentReviewDecision.Rejected);
        review.RejectionReason.Should().Be("Upload a current tax certificate.");
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private RequestVendorDocumentsCommandHandler CreateHandler() =>
        new(
            _vendorRepositoryMock.Object,
            _vendorReviewAuditServiceMock.Object,
            _vendorCommunicationServiceMock.Object,
            _vendorReadServiceMock.Object,
            _currentUserServiceMock.Object,
            _unitOfWorkMock.Object);

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
            AccountStatus: "Pending",
            IsLoginLocked: false,
            LockedAtUtc: null,
            ArchivedAtUtc: null,
            SuspendedAtUtc: null,
            RejectionReason: vendor.RejectionReason,
            SuspensionReason: null,
            LockReason: null,
            ArchiveReason: null,
            LogoUrl: vendor.LogoUrl,
            CommercialRegisterDocumentUrl: vendor.CommercialRegisterDocumentUrl,
            TaxDocumentUrl: vendor.TaxDocumentUrl,
            LicenseDocumentUrl: vendor.LicenseDocumentUrl,
            ApprovedAtUtc: null,
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
