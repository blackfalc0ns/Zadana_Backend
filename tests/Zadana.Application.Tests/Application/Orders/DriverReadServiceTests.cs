using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text.Json;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverReadServiceTests
{
    [Fact]
    public async Task GetAssignmentDetailAsync_ShouldReturnOperationalSnapshotForDriver()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var driverUser = new User("Driver Detail User", "driver.detail@test.com", "01000000055", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Motorcycle, "12345678901234", "LIC-100", address: "Riyadh")
        {
        };
        var vendor = CreateVendor();
        var branch = CreateBranch(vendor.Id);
        var address = CreateCustomerAddress(customer.Id);
        var order = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.DriverAssigned, "ORD-DETAIL-01");
        var assignment = new DeliveryAssignment(order.Id, 60m);

        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkArrivedAtVendor();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(2));

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetAssignmentDetailAsync(driver.Id, assignment.Id);

        result.Should().NotBeNull();
        result!.AssignmentStatus.Should().Be(nameof(AssignmentStatus.ArrivedAtVendor));
        result.HomeState.Should().Be("OnMission");
        result.AllowedActions.Should().ContainSingle().Which.Should().Be("verify_pickup_otp");
        result.PickupOtpRequired.Should().BeTrue();
        result.PickupOtpStatus.Should().Be("pending");
        result.DriverArrivalState.Should().Be("arrived_at_vendor");
        result.CustomerPhone.Should().Be(address.ContactPhone);
        result.OrderItems.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAssignmentDetailAsync_AfterPickupHandoff_ShouldExposeOnTheWayAction()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var driverUser = new User("Driver Detail User", "driver.detail.ontheway@test.com", "01000000062", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Motorcycle, "12345678901237", "LIC-103", address: "Riyadh");
        var vendor = CreateVendor();
        var branch = CreateBranch(vendor.Id);
        var address = CreateCustomerAddress(customer.Id);
        var order = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.PickedUp, "ORD-DETAIL-02");
        var assignment = new DeliveryAssignment(order.Id, 60m);

        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        assignment.MarkArrivedAtVendor();
        assignment.EnsurePickupOtp(TimeSpan.FromHours(2));
        assignment.VerifyPickupOtp(driver.Id, assignment.PickupOtpCode!);
        assignment.MarkPickedUp();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetAssignmentDetailAsync(driver.Id, assignment.Id);

        result.Should().NotBeNull();
        result!.AssignmentStatus.Should().Be(nameof(AssignmentStatus.PickedUp));
        result.AllowedActions.Should().ContainSingle().Which.Should().Be("mark_on_the_way");
        result.PickupOtpRequired.Should().BeFalse();
        result.PickupOtpStatus.Should().Be("verified");
        result.PickupOtpCode.Should().BeNull();
        result.DriverArrivalState.Should().Be("en_route");
    }

    [Fact]
    public async Task GetCompletedOrdersAsync_ShouldReturnOnlyCompletedAndRespectFilter()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var driverUser = new User("Driver Completed User", "driver.completed@test.com", "01000000056", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "12345678901235", "LIC-101", address: "Riyadh");
        var vendor = CreateVendor();
        var branch = CreateBranch(vendor.Id);
        var address = CreateCustomerAddress(customer.Id);

        var deliveredOrder = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.Delivered, "ORD-COMP-1");
        var cancelledOrder = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.Cancelled, "ORD-COMP-2");
        var failedOrder = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.DeliveryFailed, "ORD-COMP-3");
        var activeOrder = CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.DriverAssigned, "ORD-ACTIVE-1");

        var deliveredAssignment = CreateCompletedAssignment(driver.Id, deliveredOrder.Id, AssignmentStatus.Delivered);
        var cancelledAssignment = CreateCompletedAssignment(driver.Id, cancelledOrder.Id, AssignmentStatus.Accepted);
        var failedAssignment = CreateCompletedAssignment(driver.Id, failedOrder.Id, AssignmentStatus.Failed);
        var activeAssignment = CreateCompletedAssignment(driver.Id, activeOrder.Id, AssignmentStatus.Accepted);

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.AddRange(deliveredOrder, cancelledOrder, failedOrder, activeOrder);
        dbContext.DeliveryAssignments.AddRange(deliveredAssignment, cancelledAssignment, failedAssignment, activeAssignment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var allCompleted = await service.GetCompletedOrdersAsync(driver.Id);
        var deliveredOnly = await service.GetCompletedOrdersAsync(driver.Id, "delivered");

        allCompleted.TotalCount.Should().Be(3);
        allCompleted.Page.Should().Be(1);
        allCompleted.PerPage.Should().Be(20);
        allCompleted.HasMore.Should().BeFalse();
        allCompleted.Items.Select(x => x.Status).Should().BeEquivalentTo(["delivered", "cancelled", "deliveryFailed"]);
        deliveredOnly.TotalCount.Should().Be(1);
        deliveredOnly.Items[0].Status.Should().Be("delivered");
    }

    [Fact]
    public async Task GetCompletedOrdersAsync_ShouldPaginateResults()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer();
        var driverUser = new User("Driver Paginated User", "driver.paginated@test.com", "01000000063", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "12345678901238", "LIC-104", address: "Riyadh");
        var vendor = CreateVendor();
        var branch = CreateBranch(vendor.Id);
        var address = CreateCustomerAddress(customer.Id);

        var orders = Enumerable.Range(1, 5)
            .Select(index => CreateOrder(customer.Id, vendor.Id, branch.Id, address.Id, OrderStatus.Delivered, $"ORD-PAGE-{index}"))
            .ToArray();

        var assignments = orders
            .Select(order => CreateCompletedAssignment(driver.Id, order.Id, AssignmentStatus.Delivered))
            .ToArray();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.AddRange(orders);
        dbContext.DeliveryAssignments.AddRange(assignments);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var firstPage = await service.GetCompletedOrdersAsync(driver.Id, "delivered", page: 1, perPage: 2);
        var secondPage = await service.GetCompletedOrdersAsync(driver.Id, "delivered", page: 2, perPage: 2);

        firstPage.TotalCount.Should().Be(5);
        firstPage.Page.Should().Be(1);
        firstPage.PerPage.Should().Be(2);
        firstPage.HasMore.Should().BeTrue();
        firstPage.Items.Should().HaveCount(2);

        secondPage.TotalCount.Should().Be(5);
        secondPage.Page.Should().Be(2);
        secondPage.PerPage.Should().Be(2);
        secondPage.HasMore.Should().BeTrue();
        secondPage.Items.Should().HaveCount(2);

        var ids = firstPage.Items.Select(x => x.Id).Concat(secondPage.Items.Select(x => x.Id)).ToArray();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetDriverProfileAsync_ShouldCalculateCompletionAndMissingRequirements()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Profile User", "driver.profile@test.com", "01000000057", UserRole.Driver);
        var driver = new Driver(user.Id, DriverVehicleType.Car, "12345678901236", "LIC-102");

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.IsProfileComplete.Should().BeFalse();
        result.CompletionPercent.Should().Be(0);
        result.MissingRequirements.Should().Contain("missing_personal_info");
        result.MissingRequirements.Should().Contain("missing_vehicle_info");
        result.MissingRequirements.Should().Contain("missing_documents");
        result.MissingRequirements.Should().Contain("missing_region_city");
        result.CanSubmitForReview.Should().BeFalse();
        result.RejectionPolicy.DailyRejections.Should().Be(0);
        result.RejectionPolicy.DailyLimit.Should().Be(3);
        result.RejectionPolicy.RemainingBeforeFreeze.Should().Be(3);
        result.RejectionPolicy.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public async Task GetDriverProfileAsync_ShouldExposeDailyRejectionsAndRemainingBeforeFreeze()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Rejection Profile User", "driver.rejection.profile@test.com", "01000000073", UserRole.Driver);
        var driver = new Driver(user.Id, DriverVehicleType.Car, "12345678901241", "LIC-111", address: "Riyadh");

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);

        for (var index = 0; index < 2; index++)
        {
            var attempt = new DeliveryOfferAttempt(Guid.NewGuid(), null, driver.Id, index + 1, DateTime.UtcNow.AddMinutes(5));
            attempt.MarkRejected("skip");
            dbContext.DeliveryOfferAttempts.Add(attempt);
        }

        await dbContext.SaveChangesAsync();

        var service = new DriverReadService(
            dbContext,
            new DriverCommitmentPolicyService(dbContext, dbContext),
            Mock.Of<INotificationService>(),
            Mock.Of<IOneSignalPushService>());
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.RejectionPolicy.DailyRejections.Should().Be(2);
        result.RejectionPolicy.DailyLimit.Should().Be(3);
        result.RejectionPolicy.RemainingBeforeFreeze.Should().Be(1);
        result.RejectionPolicy.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public async Task GetDriverProfileAsync_WhenDocumentChangePending_ShouldExposeDocumentUnderReview()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Pending Document User", "driver.pending.document@test.com", "01000000074", UserRole.Driver);
        var driver = CreateCompleteDriver(user.Id);
        ApproveRequiredDocuments(driver);

        var payload = new DriverDocumentsProfileChangePayload(
            driver.Id,
            driver.NationalIdFrontImageUrl,
            driver.NationalIdBackImageUrl,
            "https://cdn.example.com/drivers/license-new.jpg",
            driver.VehicleImageUrl,
            driver.PersonalPhotoUrl);

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        dbContext.AccessApprovalRequests.Add(new AccessApprovalRequest(
            user.Id,
            user.Id,
            ProfileChangeApprovalActions.DriverProfileDocuments,
            "Driver requested document changes.",
            "pending-hash",
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.Documents.Single(document => document.DocumentType == nameof(DriverDocumentType.DriverLicense))
            .Status.Should().Be("review");
        result.Sections.Single(section => section.Section == "documents")
            .Status.Should().Be("review");
    }

    [Fact]
    public async Task GetDriverProfileAsync_WhenDocumentChangeRejected_ShouldExposeRejectedDocumentAndReason()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Rejected Document User", "driver.rejected.document@test.com", "01000000075", UserRole.Driver);
        var driver = CreateCompleteDriver(user.Id);
        ApproveRequiredDocuments(driver);

        var payload = new DriverDocumentsProfileChangePayload(
            driver.Id,
            driver.NationalIdFrontImageUrl,
            driver.NationalIdBackImageUrl,
            driver.LicenseImageUrl,
            "https://cdn.example.com/drivers/vehicle-new.jpg",
            driver.PersonalPhotoUrl);
        var approval = new AccessApprovalRequest(
            user.Id,
            user.Id,
            ProfileChangeApprovalActions.DriverProfileDocuments,
            "Driver requested document changes.",
            "rejected-hash",
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        approval.Reject(Guid.NewGuid(), "الصورة غير واضحة");

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        dbContext.AccessApprovalRequests.Add(approval);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);
        var vehicleDocument = result!.Documents.Single(document => document.DocumentType == nameof(DriverDocumentType.VehicleLicense));

        vehicleDocument.Status.Should().Be("rejected");
        vehicleDocument.RejectionReason.Should().Be("الصورة غير واضحة");
        result!.Sections.Single(section => section.Section == "documents")
            .Status.Should().Be("rejected");
    }

    [Fact]
    public async Task GetDriverProfileAsync_WhenPersonalChangePending_ShouldExposePersonalSectionUnderReview()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Pending Personal User", "driver.pending.personal@test.com", "01000000079", UserRole.Driver);
        var driver = CreateCompleteDriver(user.Id);
        ApproveRequiredDocuments(driver);

        var payload = new DriverPersonalProfileChangePayload(
            driver.Id,
            "Updated Name",
            "updated@test.com",
            "0500000001",
            "Updated Address");

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        dbContext.AccessApprovalRequests.Add(new AccessApprovalRequest(
            user.Id,
            user.Id,
            ProfileChangeApprovalActions.DriverProfilePersonal,
            "Driver requested personal changes.",
            "pending-personal-hash",
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.Sections.Single(section => section.Section == "personal").Status.Should().Be("review");
        result.Sections.Single(section => section.Section == "vehicle").Status.Should().Be("valid");
    }

    [Fact]
    public async Task GetDriverProfileAsync_WhenVehicleChangePending_ShouldExposeVehicleSectionUnderReview()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Pending Vehicle Profile User", "driver.pending.vehicle.profile@test.com", "01000000080", UserRole.Driver);
        var driver = CreateCompleteDriver(user.Id);
        ApproveRequiredDocuments(driver);

        var payload = new DriverVehicleProfileChangePayload(
            driver.Id,
            nameof(DriverVehicleType.Truck),
            "PENDING-NATIONAL-ID",
            "PENDING-LICENSE",
            DateTime.UtcNow.Date.AddYears(2),
            DateTime.UtcNow.Date.AddYears(3),
            "PENDING-PLATE",
            DateTime.UtcNow.Date.AddYears(4),
            "EASTERN",
            "DAMMAM");

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        dbContext.AccessApprovalRequests.Add(new AccessApprovalRequest(
            user.Id,
            user.Id,
            ProfileChangeApprovalActions.DriverProfileVehicle,
            "Driver requested vehicle changes.",
            "pending-vehicle-profile-hash",
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.Sections.Single(section => section.Section == "vehicle").Status.Should().Be("review");
        result.Sections.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetDriverProfileAsync_WhenNoPendingChanges_ShouldExposeValidSectionStatuses()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Valid Sections User", "driver.valid.sections@test.com", "01000000081", UserRole.Driver);
        var driver = CreateCompleteDriver(user.Id);
        ApproveRequiredDocuments(driver);

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.Sections.Should().HaveCount(3);
        result.Sections.Should().OnlyContain(section => section.Status == "valid");
    }

    [Fact]
    public async Task GetDriverProfileAsync_WhenPersonalChangeApproved_ShouldExposeValidSections()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Approved Personal User", "driver.approved.personal@test.com", "01000000082", UserRole.Driver);
        var driver = CreateCompleteDriver(user.Id);
        ApproveRequiredDocuments(driver);

        var payload = new DriverPersonalProfileChangePayload(
            driver.Id,
            "Updated Name",
            "updated@test.com",
            "0500000002",
            "Updated Address");
        var approval = new AccessApprovalRequest(
            user.Id,
            user.Id,
            ProfileChangeApprovalActions.DriverProfilePersonal,
            "Driver requested personal changes.",
            "approved-personal-hash",
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        approval.Approve(Guid.NewGuid(), "Approved");
        approval.Consume();

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        dbContext.AccessApprovalRequests.Add(approval);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.VerificationStatus.Should().Be(nameof(DriverVerificationStatus.Approved));
        result.AccountStatus.Should().Be(nameof(AccountStatus.Active));
        result.Sections.Should().OnlyContain(section => section.Status == "valid");
    }

    [Fact]
    public async Task GetDriverProfileAsync_WhenPersonalChangeRejected_ShouldExposeRejectedPersonalSection()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Rejected Personal User", "driver.rejected.personal@test.com", "01000000083", UserRole.Driver);
        var driver = CreateCompleteDriver(user.Id);
        ApproveRequiredDocuments(driver);

        var payload = new DriverPersonalProfileChangePayload(
            driver.Id,
            "Updated Name",
            "updated@test.com",
            "0500000003",
            "Updated Address");
        var approval = new AccessApprovalRequest(
            user.Id,
            user.Id,
            ProfileChangeApprovalActions.DriverProfilePersonal,
            "Driver requested personal changes.",
            "rejected-personal-hash",
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        approval.Reject(Guid.NewGuid(), "البيانات غير مطابقة");

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        dbContext.AccessApprovalRequests.Add(approval);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.Sections.Single(section => section.Section == "personal").Status.Should().Be("rejected");
        result.Sections.Single(section => section.Section == "vehicle").Status.Should().Be("valid");
    }

    [Fact]
    public async Task GetAdminDriverDetailAsync_WhenDocumentChangePending_ShouldExposePendingDocumentPreview()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Pending Admin Doc User", "driver.pending.admin@test.com", "01000000076", UserRole.Driver);
        user.VerifyEmail();
        var driver = CreateCompleteDriver(user.Id);
        ApproveRequiredDocuments(driver);

        const string pendingLicenseUrl = "https://cdn.example.com/drivers/license-new.jpg";
        var payload = new DriverDocumentsProfileChangePayload(
            driver.Id,
            driver.NationalIdFrontImageUrl,
            driver.NationalIdBackImageUrl,
            pendingLicenseUrl,
            driver.VehicleImageUrl,
            driver.PersonalPhotoUrl);

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        dbContext.AccessApprovalRequests.Add(new AccessApprovalRequest(
            user.Id,
            user.Id,
            ProfileChangeApprovalActions.DriverProfileDocuments,
            "Driver requested document changes.",
            "pending-admin-hash",
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetAdminDriverDetailAsync(driver.Id);

        result.Should().NotBeNull();
        var licenseDocument = result!.Documents.Single(document => document.DocumentType == "DriverLicense");
        licenseDocument.ImageUrl.Should().Be(pendingLicenseUrl);
        licenseDocument.Status.Should().Be("review");
        user.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task GetAdminDriverDetailAsync_WhenVehicleChangePending_ShouldExposePendingProfileValues()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Pending Vehicle User", "driver.pending.vehicle@test.com", "01000000077", UserRole.Driver);
        var driver = CreateCompleteDriver(user.Id);

        var payload = new DriverVehicleProfileChangePayload(
            driver.Id,
            nameof(DriverVehicleType.Truck),
            "PENDING-NATIONAL-ID",
            "PENDING-LICENSE",
            DateTime.UtcNow.Date.AddYears(2),
            DateTime.UtcNow.Date.AddYears(3),
            "PENDING-PLATE",
            DateTime.UtcNow.Date.AddYears(4),
            "EASTERN",
            "DAMMAM");

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        dbContext.AccessApprovalRequests.Add(new AccessApprovalRequest(
            user.Id,
            user.Id,
            ProfileChangeApprovalActions.DriverProfileVehicle,
            "Driver requested vehicle changes.",
            "pending-vehicle-hash",
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetAdminDriverDetailAsync(driver.Id);

        result.Should().NotBeNull();
        result!.VehicleType.Should().Be(DriverVehicleType.Truck);
        result.NationalId.Should().Be(payload.NationalId);
        result.LicenseNumber.Should().Be(payload.LicenseNumber);
        result.VehicleLicenseNumber.Should().Be(payload.VehicleLicenseNumber);
        result.Operations.Region.Should().Be(payload.Region);
        result.Operations.City.Should().Be(payload.City);
        result.Documents.Single(document => document.DocumentType == "NationalId").Number.Should().Be(payload.NationalId);
        result.Documents.Single(document => document.DocumentType == "DriverLicense").Number.Should().Be(payload.LicenseNumber);
        result.Documents.Single(document => document.DocumentType == "VehicleLicense").Number.Should().Be(payload.VehicleLicenseNumber);
    }

    [Fact]
    public async Task GetAdminDriverDetailAsync_WhenEncryptedValuesCannotBeRead_ShouldRecoverFromApprovedVehicleChange()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Driver Approved Vehicle User", "driver.approved.vehicle@test.com", "01000000078", UserRole.Driver);
        var driver = CreateCompleteDriver(user.Id);

        var payload = new DriverVehicleProfileChangePayload(
            driver.Id,
            driver.VehicleType?.ToString(),
            "RECOVERED-NATIONAL-ID",
            "RECOVERED-LICENSE",
            driver.NationalIdExpiryDate,
            driver.DriverLicenseExpiryDate,
            "RECOVERED-PLATE",
            driver.VehicleLicenseExpiryDate,
            driver.Region,
            driver.City);
        var approval = new AccessApprovalRequest(
            user.Id,
            user.Id,
            ProfileChangeApprovalActions.DriverProfileVehicle,
            "Driver requested vehicle changes.",
            "approved-vehicle-hash",
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        approval.Approve(Guid.NewGuid(), "Approved");

        // Simulate encrypted database values that can no longer be decrypted.
        driver.UpdateDetails(
            vehicleType: driver.VehicleType,
            nationalId: null,
            licenseNumber: null,
            nationalIdExpiryDate: driver.NationalIdExpiryDate,
            driverLicenseExpiryDate: driver.DriverLicenseExpiryDate,
            vehicleLicenseNumber: null,
            vehicleLicenseExpiryDate: driver.VehicleLicenseExpiryDate);

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        dbContext.AccessApprovalRequests.Add(approval);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetAdminDriverDetailAsync(driver.Id);

        result.Should().NotBeNull();
        result!.NationalId.Should().Be(payload.NationalId);
        result.LicenseNumber.Should().Be(payload.LicenseNumber);
        result.VehicleLicenseNumber.Should().Be(payload.VehicleLicenseNumber);
        result.Documents.Single(document => document.DocumentType == "NationalId").Number.Should().Be(payload.NationalId);
        result.Documents.Single(document => document.DocumentType == "DriverLicense").Number.Should().Be(payload.LicenseNumber);
        result.Documents.Single(document => document.DocumentType == "VehicleLicense").Number.Should().Be(payload.VehicleLicenseNumber);
    }

    [Fact]
    public async Task GetAdminDriverDetailAsync_ShouldExposeLocationAccessState()
    {
        await using var dbContext = CreateDbContext();
        var driverUser = new User("Driver Ops User", "driver.ops@test.com", "01000000071", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "12345678901239", "LIC-109", address: "Riyadh");
        driver.Approve(Guid.NewGuid());
        driver.BlockLocationUpdates(Guid.NewGuid(), "manual ops hold");

        dbContext.Users.Add(driverUser);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetAdminDriverDetailAsync(driver.Id);

        result.Should().NotBeNull();
        result!.Operations.LocationUpdatesBlocked.Should().BeTrue();
        result.Operations.LocationBlockReason.Should().Be("manual ops hold");
        result.Operations.LocationBlockedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDriverProfileAsync_WhenRequiredDocumentExpired_ShouldLockDriverAccount()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Expired Driver Profile User", "expired.driver.profile@test.com", "01000000072", UserRole.Driver);
        var driver = new Driver(
            user.Id,
            DriverVehicleType.Car,
            "12345678901240",
            "LIC-110",
            nationalIdExpiryDate: DateTime.UtcNow.Date.AddDays(-1),
            driverLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            vehicleLicenseNumber: "VEH-110",
            vehicleLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            address: "Riyadh",
            nationalIdFrontImageUrl: "https://cdn.example.com/drivers/id-front.jpg",
            nationalIdBackImageUrl: "https://cdn.example.com/drivers/id-back.jpg",
            licenseImageUrl: "https://cdn.example.com/drivers/license.jpg",
            vehicleImageUrl: "https://cdn.example.com/drivers/vehicle.jpg",
            personalPhotoUrl: "https://cdn.example.com/drivers/photo.jpg",
            region: "RIYADH",
            city: "RIYADH");
        driver.Approve(Guid.NewGuid());

        dbContext.Users.Add(user);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetDriverProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.AccountStatus.Should().Be(nameof(AccountStatus.Inactive));
        result.VerificationStatus.Should().Be(nameof(DriverVerificationStatus.NeedsDocuments));
        result.MissingRequirements.Should().Contain("expired_documents");

        driver.Status.Should().Be(AccountStatus.Inactive);
        driver.VerificationStatus.Should().Be(DriverVerificationStatus.NeedsDocuments);
    }

    private static DriverReadService CreateService(ApplicationDbContext dbContext)
    {
        var commitmentPolicy = new Mock<IDriverCommitmentPolicyService>();
        commitmentPolicy
            .Setup(x => x.GetDriverSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverCommitmentSummaryDto(0, 0, 0, 0, 0, 100m, "Healthy", true, null, null));

        commitmentPolicy
            .Setup(x => x.GetDriverSummariesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DriverCommitmentSummaryDto>());

        return new DriverReadService(
            dbContext,
            commitmentPolicy.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IOneSignalPushService>());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateCustomer() =>
        new("Customer User", "driver.read.customer@test.com", "01000000058", UserRole.Customer);

    private static Driver CreateCompleteDriver(Guid userId)
    {
        var driver = new Driver(
            userId,
            DriverVehicleType.Car,
            "12345678901242",
            "LIC-112",
            nationalIdExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            driverLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            vehicleLicenseNumber: "VEH-112",
            vehicleLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            address: "Riyadh",
            nationalIdFrontImageUrl: "https://cdn.example.com/drivers/id-front.jpg",
            nationalIdBackImageUrl: "https://cdn.example.com/drivers/id-back.jpg",
            licenseImageUrl: "https://cdn.example.com/drivers/license.jpg",
            vehicleImageUrl: "https://cdn.example.com/drivers/vehicle.jpg",
            personalPhotoUrl: "https://cdn.example.com/drivers/photo.jpg",
            region: "RIYADH",
            city: "RIYADH");
        driver.Approve(Guid.NewGuid());
        return driver;
    }

    private static void ApproveRequiredDocuments(Driver driver)
    {
        driver.GetOrCreateDocumentReview(DriverDocumentType.NationalId).Approve(Guid.NewGuid(), "Admin");
        driver.GetOrCreateDocumentReview(DriverDocumentType.DriverLicense).Approve(Guid.NewGuid(), "Admin");
        driver.GetOrCreateDocumentReview(DriverDocumentType.VehicleLicense).Approve(Guid.NewGuid(), "Admin");
    }

    private static Vendor CreateVendor() =>
        new(
            Guid.NewGuid(),
            "متجر تجريبي",
            "Driver Read Vendor",
            "Groceries",
            $"CR-{Guid.NewGuid():N}".Substring(0, 12),
            $"vendor-{Guid.NewGuid():N}@test.com",
            "01000000059",
            city: "Riyadh",
            nationalAddress: "Olaya");

    private static VendorBranch CreateBranch(Guid vendorId) =>
        new(vendorId, "Main Branch", "Olaya Street", 24.7136m, 46.6753m, "01000000060", 12m);

    private static CustomerAddress CreateCustomerAddress(Guid userId) =>
        new(userId, "Ahmed Customer", "01000000061", "Yasmin District", AddressLabel.Home, city: "Riyadh", area: "Yasmin", latitude: 24.7821m, longitude: 46.6520m);

    private static Order CreateOrder(
        Guid userId,
        Guid vendorId,
        Guid vendorBranchId,
        Guid customerAddressId,
        OrderStatus status,
        string orderNumber)
    {
        var order = new Order(
            orderNumber,
            userId,
            vendorId,
            customerAddressId,
            PaymentMethodType.CashOnDelivery,
            100m,
            0m,
            12m,
            10m,
            2m,
            0m,
            4.6m,
            "exact-distance",
            "Riyadh Standard",
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            1,
            false,
            5m,
            vendorBranchId: vendorBranchId);

        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Fresh Item", 2, 50m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }

    private static DeliveryAssignment CreateCompletedAssignment(Guid driverId, Guid orderId, AssignmentStatus status)
    {
        var assignment = new DeliveryAssignment(orderId, 50m);
        assignment.OfferTo(driverId, 1, DateTime.UtcNow.AddMinutes(5));

        if (status != AssignmentStatus.Cancelled)
        {
            assignment.Accept();
        }

        return status switch
        {
            AssignmentStatus.Delivered => MarkDelivered(assignment),
            AssignmentStatus.Failed => MarkFailed(assignment),
            _ => assignment
        };
    }

    private static DeliveryAssignment MarkDelivered(DeliveryAssignment assignment)
    {
        assignment.MarkPickedUp();
        assignment.MarkDelivered();
        return assignment;
    }

    private static DeliveryAssignment MarkFailed(DeliveryAssignment assignment)
    {
        assignment.Fail("delivery failed");
        return assignment;
    }

}
