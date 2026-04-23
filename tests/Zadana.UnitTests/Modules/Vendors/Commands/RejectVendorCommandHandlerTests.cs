using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.RejectVendor;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class RejectVendorCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IVendorReviewAuditService> _vendorReviewAuditServiceMock = new();
    private readonly Mock<IVendorRepository> _vendorRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task Handle_WithValidRequest_RejectsVendor()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "t@t.com", "1");
        _currentUserServiceMock.Setup(service => service.UserId).Returns((Guid?)null);
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var handler = new RejectVendorCommandHandler(
            _vendorRepositoryMock.Object,
            _vendorReviewAuditServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        await handler.Handle(new RejectVendorCommand(vendor.Id, "Missing docs"), default);

        vendor.Status.Should().Be(VendorStatus.Rejected);
        vendor.RejectionReason.Should().Be("Missing docs");
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidVendorId_ThrowsNotFoundException()
    {
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vendor?)null);

        var handler = new RejectVendorCommandHandler(
            _vendorRepositoryMock.Object,
            _vendorReviewAuditServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RejectVendorCommand(Guid.NewGuid(), "reason"), default));
    }

    [Fact]
    public async Task Handle_WhenVendorIsAlreadyActive_ThrowsBusinessRuleException()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "t@t.com", "1");
        vendor.DocumentReviews.Add(new VendorDocumentReview(vendor.Id, VendorDocumentType.Commercial));
        vendor.DocumentReviews.Add(new VendorDocumentReview(vendor.Id, VendorDocumentType.Tax));
        vendor.DocumentReviews.Add(new VendorDocumentReview(vendor.Id, VendorDocumentType.License));
        vendor.Approve(10, Guid.NewGuid());

        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var handler = new RejectVendorCommandHandler(
            _vendorRepositoryMock.Object,
            _vendorReviewAuditServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new RejectVendorCommand(vendor.Id, "reason"), default));

        exception.Message.Should().Contain("Use suspension");
        vendor.Status.Should().Be(VendorStatus.Active);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
