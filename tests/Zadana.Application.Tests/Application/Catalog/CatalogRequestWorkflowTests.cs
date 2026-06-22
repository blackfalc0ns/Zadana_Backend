using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Commands.CategoryRequests.ReviewRequest;
using Zadana.Application.Modules.Catalog.Commands.CategoryRequests.SubmitRequest;
using Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;
using Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;
using Zadana.Application.Modules.Catalog.Commands.BrandRequests.ReviewRequest;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Catalog;

public class CatalogRequestWorkflowTests
{
    [Fact]
    public async Task ReviewProductRequest_WhenCreatingNewProduct_ShouldPublishAndGenerateUniqueSlug()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedVendorFixtureAsync(dbContext);
        var category = new Category("فئة", "Category", null, fixture.ParentCategory.Id, 1);
        dbContext.Categories.Add(category);
        dbContext.MasterProducts.Add(new MasterProduct("قديمة", "Other Name", "test-product", category.Id));

        var request = new ProductRequest(
            fixture.Vendor.Id,
            "منتج جديد",
            "Test Product",
            suggestedCategoryId: category.Id);

        dbContext.ProductRequests.Add(request);
        await dbContext.SaveChangesAsync();

        var handler = new ReviewProductRequestCommandHandler(
            dbContext,
            Mock.Of<ICacheInvalidator>(),
            new StubCurrentUserService(fixture.AdminUser.Id, UserRole.Admin),
            new StubIdentityAccountService(fixture.AdminUser),
            new PassThroughLocalizer<SharedResource>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<ILogger<ReviewProductRequestCommandHandler>>());

        var createdId = await handler.Handle(new ReviewProductRequestCommand(request.Id, true, null), CancellationToken.None);

        createdId.Should().NotBeNull();
        var created = await dbContext.MasterProducts.SingleAsync(item => item.Id == createdId!.Value);
        created.Status.Should().Be(ProductStatus.Active);
        created.Slug.Should().Be("test-product-2");
    }

    [Fact]
    public async Task ReviewProductRequest_WhenMatchingProductExists_ShouldReuseAndPublishExistingProduct()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedVendorFixtureAsync(dbContext);
        var category = new Category("فئة", "Category", null, fixture.ParentCategory.Id, 1);
        dbContext.Categories.Add(category);
        var existing = new MasterProduct("منتج", "Reusable Product", "reusable-product", category.Id);
        dbContext.MasterProducts.Add(existing);
        await dbContext.SaveChangesAsync();

        var request = new ProductRequest(
            fixture.Vendor.Id,
            "منتج",
            "Reusable Product",
            suggestedCategoryId: category.Id);

        dbContext.ProductRequests.Add(request);
        await dbContext.SaveChangesAsync();

        var handler = new ReviewProductRequestCommandHandler(
            dbContext,
            Mock.Of<ICacheInvalidator>(),
            new StubCurrentUserService(fixture.AdminUser.Id, UserRole.Admin),
            new StubIdentityAccountService(fixture.AdminUser),
            new PassThroughLocalizer<SharedResource>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<ILogger<ReviewProductRequestCommandHandler>>());

        var createdId = await handler.Handle(new ReviewProductRequestCommand(request.Id, true, null), CancellationToken.None);

        createdId.Should().Be(existing.Id);
        dbContext.MasterProducts.Count().Should().Be(1);
        existing.Status.Should().Be(ProductStatus.Active);
    }

    [Fact]
    public async Task ReviewBrandRequest_WhenMatchingBrandExists_ShouldReuseBrandAndAddCategoryLink()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedVendorFixtureAsync(dbContext);
        var firstCategory = new Category("فئة 1", "Category One", null, fixture.ParentCategory.Id, 1);
        var secondCategory = new Category("فئة 2", "Category Two", null, fixture.ParentCategory.Id, 2);
        dbContext.Categories.AddRange(firstCategory, secondCategory);
        var existingBrand = new Brand("براند", "Shared Brand", null, null, firstCategory.Id);
        dbContext.Brands.Add(existingBrand);
        await dbContext.SaveChangesAsync();

        var request = new BrandRequest(fixture.Vendor.Id, secondCategory.Id, "براند", "Shared Brand");
        dbContext.BrandRequests.Add(request);
        await dbContext.SaveChangesAsync();

        var handler = new ReviewBrandRequestCommandHandler(
            dbContext,
            Mock.Of<ICacheInvalidator>(),
            new StubCurrentUserService(fixture.AdminUser.Id, UserRole.Admin),
            new StubIdentityAccountService(fixture.AdminUser),
            new PassThroughLocalizer<SharedResource>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<ILogger<ReviewBrandRequestCommandHandler>>());

        var createdId = await handler.Handle(new ReviewBrandRequestCommand(request.Id, true, null), CancellationToken.None);

        createdId.Should().Be(existingBrand.Id);
        dbContext.Brands.Count().Should().Be(1);
        dbContext.BrandCategories.Should().ContainSingle(link =>
            link.BrandId == existingBrand.Id &&
            link.CategoryId == secondCategory.Id);
    }

    [Fact]
    public async Task ReviewCategoryRequest_WhenMatchingCategoryExists_ShouldReuseExistingCategory()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedVendorFixtureAsync(dbContext);
        var existing = new Category("فرعي", "Reusable Category", null, fixture.ParentCategory.Id, 1);
        existing.Deactivate();
        dbContext.Categories.Add(existing);
        await dbContext.SaveChangesAsync();

        var request = new CategoryRequest(
            fixture.Vendor.Id,
            "فرعي",
            "Reusable Category",
            "sub_category",
            fixture.ParentCategory.Id,
            2);

        dbContext.CategoryRequests.Add(request);
        await dbContext.SaveChangesAsync();

        var handler = new ReviewCategoryRequestCommandHandler(
            dbContext,
            Mock.Of<ICacheInvalidator>(),
            new StubCurrentUserService(fixture.AdminUser.Id, UserRole.Admin),
            new StubIdentityAccountService(fixture.AdminUser),
            new PassThroughLocalizer<SharedResource>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<ILogger<ReviewCategoryRequestCommandHandler>>());

        var createdId = await handler.Handle(new ReviewCategoryRequestCommand(request.Id, true, null, null, null), CancellationToken.None);

        createdId.Should().Be(existing.Id);
        dbContext.Categories.Count().Should().Be(2);
        existing.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitCategoryRequest_WhenDuplicatePendingExists_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedVendorFixtureAsync(dbContext);
        dbContext.CategoryRequests.Add(new CategoryRequest(
            fixture.Vendor.Id,
            "مكرر",
            "Duplicate",
            "sub_category",
            fixture.ParentCategory.Id,
            1));
        await dbContext.SaveChangesAsync();

        var handler = new SubmitCategoryRequestCommandHandler(
            dbContext,
            new StubCurrentVendorService(fixture.Vendor.Id),
            new PassThroughLocalizer<SharedResource>(),
            Mock.Of<IAdminAlertService>());

        var act = () => handler.Handle(
            new SubmitCategoryRequestCommand("مكرر", "Duplicate", "sub_category", fixture.ParentCategory.Id, 2),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.ErrorCode == "CATEGORY_REQUEST_ALREADY_PENDING");
    }

    [Fact]
    public async Task SubmitProductRequest_WhenRequestedBrandDuplicatePendingExists_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedVendorFixtureAsync(dbContext);
        var category = new Category("ÙØ¦Ø©", "Category", null, fixture.ParentCategory.Id, 1);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        dbContext.BrandRequests.Add(new BrandRequest(
            fixture.Vendor.Id,
            category.Id,
            "Ø¨Ø±Ø§Ù†Ø¯",
            "Brand"));
        await dbContext.SaveChangesAsync();

        var handler = new SubmitProductRequestCommandHandler(
            dbContext,
            new StubCurrentVendorService(fixture.Vendor.Id),
            new PassThroughLocalizer<SharedResource>(),
            Mock.Of<IAdminAlertService>());

        var act = () => handler.Handle(
            new SubmitProductRequestCommand(
                "Ù…Ù†ØªØ¬",
                "Product",
                SuggestedCategoryId: fixture.ParentCategory.Id,
                RequestedBrand: new RequestedBrandDraft(category.Id, "Ø¨Ø±Ø§Ù†Ø¯", "Brand")),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.ErrorCode == "BRAND_REQUEST_ALREADY_PENDING");
    }

    [Fact]
    public async Task SubmitProductRequest_WhenRequestedCategoryAlreadyExists_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedVendorFixtureAsync(dbContext);
        dbContext.Categories.Add(new Category("Ù…ÙƒØ±Ø±", "Duplicate", null, fixture.ParentCategory.Id, 1));
        await dbContext.SaveChangesAsync();

        var handler = new SubmitProductRequestCommandHandler(
            dbContext,
            new StubCurrentVendorService(fixture.Vendor.Id),
            new PassThroughLocalizer<SharedResource>(),
            Mock.Of<IAdminAlertService>());

        var act = () => handler.Handle(
            new SubmitProductRequestCommand(
                "Ù…Ù†ØªØ¬",
                "Product",
                RequestedCategory: new RequestedCategoryDraft(
                    "Ù…ÙƒØ±Ø±",
                    "Duplicate",
                    "sub_category",
                    fixture.ParentCategory.Id)),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.ErrorCode == "CATEGORY_ALREADY_EXISTS");
    }

    [Fact]
    public async Task SubmitProductRequest_WhenActiveCatalogProductExists_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedVendorFixtureAsync(dbContext);
        var category = new Category("فئة", "Category", null, fixture.ParentCategory.Id, 1);
        dbContext.Categories.Add(category);
        var existing = new MasterProduct("منتج", "Existing Product", "existing-product", category.Id);
        existing.Publish();
        dbContext.MasterProducts.Add(existing);
        await dbContext.SaveChangesAsync();

        var handler = new SubmitProductRequestCommandHandler(
            dbContext,
            new StubCurrentVendorService(fixture.Vendor.Id),
            new PassThroughLocalizer<SharedResource>(),
            Mock.Of<IAdminAlertService>());

        var act = () => handler.Handle(
            new SubmitProductRequestCommand(
                "منتج",
                "Existing Product",
                SuggestedCategoryId: category.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.ErrorCode == "PRODUCT_ALREADY_EXISTS");
    }

    [Fact]
    public async Task ReviewProductRequest_WhenMatchingProductIsDiscontinued_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedVendorFixtureAsync(dbContext);
        var category = new Category("ÙØ¦Ø©", "Category", null, fixture.ParentCategory.Id, 1);
        dbContext.Categories.Add(category);
        var existing = new MasterProduct("Ù…Ù†ØªØ¬", "Archived Product", "archived-product", category.Id);
        existing.Discontinue();
        dbContext.MasterProducts.Add(existing);
        await dbContext.SaveChangesAsync();

        var request = new ProductRequest(
            fixture.Vendor.Id,
            "Ù…Ù†ØªØ¬",
            "Archived Product",
            suggestedCategoryId: category.Id);

        dbContext.ProductRequests.Add(request);
        await dbContext.SaveChangesAsync();

        var handler = new ReviewProductRequestCommandHandler(
            dbContext,
            Mock.Of<ICacheInvalidator>(),
            new StubCurrentUserService(fixture.AdminUser.Id, UserRole.Admin),
            new StubIdentityAccountService(fixture.AdminUser),
            new PassThroughLocalizer<SharedResource>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<ILogger<ReviewProductRequestCommandHandler>>());

        var act = () => handler.Handle(new ReviewProductRequestCommand(request.Id, true, null), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(ex => ex.ErrorCode == "PRODUCT_DISCONTINUED");

        request.Status.Should().Be(ApprovalStatus.Pending);
        existing.Status.Should().Be(ProductStatus.Discontinued);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static async Task<TestFixture> SeedVendorFixtureAsync(ApplicationDbContext dbContext)
    {
        var adminUser = new User("Admin User", "admin@test.com", "0500000000", UserRole.Admin);
        var vendorUser = new User("Vendor User", "vendor@test.com", "0511111111", UserRole.Vendor);
        var vendor = new Vendor(
            vendorUser.Id,
            "متجر",
            "Store",
            "Retail",
            "1234567890",
            "vendor@test.com",
            "0511111111");
        var activity = new Category("نشاط", "Activity", null, null, 1);
        var subActivity = new Category("نشاط فرعي", "Sub Activity", null, activity.Id, 1);
        var parentCategory = new Category("تصنيف أب", "Parent Category", null, subActivity.Id, 1);

        dbContext.Users.AddRange(adminUser, vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.Categories.AddRange(activity, subActivity, parentCategory);
        await dbContext.SaveChangesAsync();

        return new TestFixture(adminUser, vendorUser, vendor, activity, subActivity, parentCategory);
    }

    private sealed record TestFixture(User AdminUser, User VendorUser, Vendor Vendor, Category Activity, Category SubActivity, Category ParentCategory);

    private sealed class StubCurrentVendorService(Guid vendorId) : ICurrentVendorService
    {
        public Task<Guid?> TryGetVendorIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(vendorId);
        public Task<Guid> GetRequiredVendorIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(vendorId);
        public Task<CurrentVendorScope?> TryGetVendorScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<CurrentVendorScope?>(new CurrentVendorScope(vendorId, null));
        public Task<CurrentVendorScope> GetRequiredVendorScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CurrentVendorScope(vendorId, null));
    }

    private sealed class StubCurrentUserService(Guid userId, UserRole role) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? GuestDeviceId => null;
        public string? Role => role.ToString();
        public bool IsAuthenticated => true;
        public string? AccessTokenJti => null;
        public DateTime? AccessTokenExpiresAtUtc => null;
        public string? GetDeviceInfo() => null;
    }

    private sealed class StubIdentityAccountService(User user) : IIdentityAccountService
    {
        public Task<IdentityAccountSnapshot?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IdentityAccountSnapshot?>(new IdentityAccountSnapshot(
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.Role,
                user.PermissionVersion,
                user.AccountStatus,
                user.IsLoginLocked,
                user.LockedAtUtc,
                user.ArchivedAtUtc,
                user.EmailConfirmed,
                user.PhoneNumberConfirmed,
                user.MustChangePassword,
                user.ProfilePhotoUrl));

        public Task<IdentityAccountSnapshot?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ExistsByIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ExistsByEmailOrPhoneAsync(string email, string phoneNumber, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityCreateResult> CreateAsync(CreateIdentityAccountRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> DeleteAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CredentialValidationResult> ValidateCredentialsAsync(string identifier, string password, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> RecordLoginAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> RecordActivityAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> UpdateProfileAsync(Guid userId, string fullName, string email, string phoneNumber, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> UpdateProfilePhotoAsync(Guid userId, string? profilePhotoUrl, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> UpdateRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> ActivateAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> SuspendAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> LockLoginAsync(Guid userId, string reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> UnlockLoginAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> ArchiveAsync(Guid userId, string reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> ResetPasswordByAdminAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OtpDispatchResult> GenerateRegistrationOtpAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OtpDispatchResult> ResendRegistrationOtpAsync(string identifier, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OtpVerificationResult> VerifyRegistrationOtpAsync(string identifier, string otpCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OtpDispatchResult> GeneratePasswordResetOtpAsync(string identifier, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PasswordResetOtpVerificationResult> VerifyPasswordResetOtpAsync(string identifier, string otpCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PasswordResetResult> CompletePasswordResetAsync(string identifier, string resetToken, string newPassword, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}
