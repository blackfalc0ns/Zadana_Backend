using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.EmailCenter;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Services;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.EmailCenter;

public class EmailCenterServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_ShouldSeedSenderProfilesAndRules()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var overview = await service.GetOverviewAsync();

        overview.SenderProfiles.Should().NotBeEmpty();
        overview.Rules.Should().Contain(rule => rule.Id == "super-admin-access-invite");
        overview.Rules.Should().Contain(rule => rule.AutomationState == "live");
    }

    [Fact]
    public async Task ResolveRecipientsAsync_WhenVendorScopeIsAmbiguous_ShouldReturnWarning()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var overview = await service.GetOverviewAsync();
        var vendorRule = overview.Rules.Single(rule => rule.Id == "vendor-branch-invite");

        var resolved = await service.ResolveRecipientsAsync(vendorRule.Id, vendorRule);

        resolved.Warnings.Should().NotBeEmpty();
        resolved.Warnings[0].Should().Contain("vendorId");
    }

    [Fact]
    public async Task TestSendAsync_WhenScopeIsMissing_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var overview = await service.GetOverviewAsync();
        var customerRule = overview.Rules.Single(rule => rule.Id == "customer-support-escalation");

        var action = () => service.TestSendAsync(customerRule.Id, customerRule);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*entityId*");
    }

    [Fact]
    public async Task PreviewTemplateAsync_ShouldRenderBackendHtmlWithSampleVariables()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var overview = await service.GetOverviewAsync();
        var orderRule = overview.Rules.First(rule => rule.Id == "customer-order-confirmed");

        var preview = await service.PreviewTemplateAsync(orderRule.Id, orderRule);

        preview.Html.Should().Contain("ZD-10482");
        preview.SubjectEn.Should().Contain("ZD-10482");
        preview.BodyEn.Should().Contain("Ahmed Al-Rashid");
        preview.Html.Should().Contain("img src=");
    }

    [Fact]
    public async Task TestSendAsync_WhenCustomerRuleIsScoped_ShouldPersistDispatchLog()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User(
            fullName: "Customer Test",
            email: "customer.test@zadana.local",
            phone: "01055550000",
            role: UserRole.Customer);
        dbContext.Users.Add(customer);
        await dbContext.SaveChangesAsync();

        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock
            .Setup(service => service.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSendResult("mock", true, "message-1", null));

        var service = CreateService(dbContext, emailServiceMock.Object);
        var overview = await service.GetOverviewAsync();
        var customerRule = overview.Rules.Single(rule => rule.Id == "customer-support-escalation");
        var scopedRule = customerRule with
        {
            EntityScope = customerRule.EntityScope with
            {
                EntityId = customer.Id.ToString()
            }
        };

        var result = await service.TestSendAsync(scopedRule.Id, scopedRule);

        result.Status.Should().Be("sent");
        dbContext.EmailDispatchLogs.Count().Should().Be(1);
        dbContext.EmailDispatchLogs.Single().IsTestSend.Should().BeTrue();
        dbContext.EmailDispatchLogs.Single().Status.Should().Be("sent");
        emailServiceMock.Verify(service => service.SendEmailAsync(
            It.Is<SendEmailRequest>(request => request.To.Contains(customer.Email!)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveRecipientsAsync_WhenAssignedAdminTargetIsUsedOutsideAdminAudience_ShouldIncludeAdminRecipient()
    {
        await using var dbContext = CreateDbContext();
        var role = new RoleDefinition("super_admin_all", "Super Admin", UserRole.SuperAdmin, PanelScope.SuperAdminPanel);
        var admin = new User(
            fullName: "Email Admin",
            email: "email.admin@zadana.local",
            phone: "01055550001",
            role: UserRole.SuperAdmin);

        dbContext.RoleDefinitions.Add(role);
        dbContext.Users.Add(admin);
        dbContext.UserAccessScopes.Add(new UserAccessScope(admin.Id, role.Id, PanelScope.SuperAdminPanel, AccessScopeType.Global));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var overview = await service.GetOverviewAsync();
        var vendorRule = overview.Rules.Single(rule => rule.Id == "vendor-branch-invite");

        var resolved = await service.ResolveRecipientsAsync(vendorRule.Id, vendorRule);

        resolved.Cc.Should().Contain(admin.Email);
    }

    [Fact]
    public async Task DispatchSystemEventEmailAsync_ShouldUseAdminRoutingAndAvoidFallbackWhenEventRecipientExists()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User(
            fullName: "Customer Event",
            email: "customer.event@zadana.local",
            phone: "01055550002",
            role: UserRole.Customer);
        dbContext.Users.Add(customer);
        await dbContext.SaveChangesAsync();

        SendEmailRequest? capturedRequest = null;
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock
            .Setup(service => service.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new EmailSendResult("mock", true, "message-1", null));

        var service = CreateService(dbContext, emailServiceMock.Object);
        var overview = await service.GetOverviewAsync();
        var customerRule = overview.Rules.Single(rule => rule.Id == "customer-order-confirmed");
        var updatedRule = customerRule with
        {
            Route = customerRule.Route with
            {
                StaticCc = ["audit@zadana.local"],
                FallbackTo = ["fallback@zadana.local"]
            }
        };

        await service.UpdateRuleAsync(updatedRule.Id, updatedRule);

        var result = await service.DispatchSystemEventEmailAsync(
            new EmailSystemEventDispatchRequest(
                EventKey: EmailEventKeys.CustomerOrderConfirmed,
                AudienceType: "customers",
                To: ["customer@zadana.local"],
                Variables: new Dictionary<string, string>
                {
                    ["order_number"] = "ORD-1001",
                    ["customer_name"] = "Customer Test",
                    ["vendor_name"] = "Vendor Test",
                    ["order_total"] = "42.00",
                    ["currency"] = "SAR"
                },
                TargetUrl: "/orders/ORD-1001",
                EntityId: Guid.NewGuid(),
                RecipientEntityId: customer.Id),
            CancellationToken.None);

        result.Sent.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.To.Should().Contain(customer.Email);
        capturedRequest.To.Should().NotContain("customer@zadana.local");
        capturedRequest.To.Should().NotContain("fallback@zadana.local");
        capturedRequest.Cc.Should().Contain("audit@zadana.local");
        dbContext.EmailDispatchLogs.Single(log => log.EventKey == EmailEventKeys.CustomerOrderConfirmed)
            .Source.Should().Be("system_event");
    }

    [Fact]
    public async Task DispatchSystemEventEmailAsync_WhenCustomerScopeDoesNotMatch_ShouldSkip()
    {
        await using var dbContext = CreateDbContext();
        var emailServiceMock = new Mock<IEmailService>();
        var service = CreateService(dbContext, emailServiceMock.Object);
        var overview = await service.GetOverviewAsync();
        var customerRule = overview.Rules.Single(rule => rule.Id == "customer-order-confirmed");
        var scopedRule = customerRule with
        {
            EntityScope = customerRule.EntityScope with
            {
                EntityId = Guid.NewGuid().ToString()
            }
        };

        await service.UpdateRuleAsync(scopedRule.Id, scopedRule);

        var result = await service.DispatchSystemEventEmailAsync(
            new EmailSystemEventDispatchRequest(
                EventKey: EmailEventKeys.CustomerOrderConfirmed,
                AudienceType: "customers",
                To: ["customer@zadana.local"],
                Variables: new Dictionary<string, string>
                {
                    ["order_number"] = "ORD-2001"
                },
                EntityId: Guid.NewGuid(),
                RecipientEntityId: Guid.NewGuid()),
            CancellationToken.None);

        result.Skipped.Should().BeTrue();
        result.Sent.Should().BeFalse();
        result.Reason.Should().Be("The live rule is scoped to another customer.");
        emailServiceMock.Verify(
            service => service.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchSystemEventEmailAsync_WhenVendorBranchScopeIsMissing_ShouldSkip()
    {
        await using var dbContext = CreateDbContext();
        var emailServiceMock = new Mock<IEmailService>();
        var service = CreateService(dbContext, emailServiceMock.Object);
        var overview = await service.GetOverviewAsync();
        var vendorRule = overview.Rules.Single(rule => rule.Id == "vendor-order-action-required");
        var scopedRule = vendorRule with
        {
            EntityScope = vendorRule.EntityScope with
            {
                VendorId = Guid.NewGuid().ToString(),
                BranchId = Guid.NewGuid().ToString()
            },
            BranchScopeMode = "specific_branch"
        };

        await service.UpdateRuleAsync(scopedRule.Id, scopedRule);

        var result = await service.DispatchSystemEventEmailAsync(
            new EmailSystemEventDispatchRequest(
                EventKey: EmailEventKeys.VendorOrderActionRequired,
                AudienceType: "vendor_network",
                To: ["vendor.branch@zadana.local"],
                VendorId: Guid.Parse(scopedRule.EntityScope.VendorId!)),
            CancellationToken.None);

        result.Skipped.Should().BeTrue();
        result.Sent.Should().BeFalse();
        result.Reason.Should().Be("The live rule requires a specific branch scope.");
        emailServiceMock.Verify(
            service => service.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchSystemEventEmailAsync_WhenBoundedCustomerRecipientIsMissing_ShouldSkipWithoutUsingFallback()
    {
        await using var dbContext = CreateDbContext();
        var emailServiceMock = new Mock<IEmailService>();
        var service = CreateService(dbContext, emailServiceMock.Object);
        var overview = await service.GetOverviewAsync();
        var customerRule = overview.Rules.Single(rule => rule.Id == "customer-order-confirmed");

        var result = await service.DispatchSystemEventEmailAsync(
            new EmailSystemEventDispatchRequest(
                EventKey: EmailEventKeys.CustomerOrderConfirmed,
                AudienceType: "customers",
                To: ["customer@zadana.local"],
                Variables: new Dictionary<string, string>
                {
                    ["order_number"] = "ORD-3001"
                },
                EntityId: Guid.NewGuid(),
                RecipientEntityId: Guid.NewGuid()),
            CancellationToken.None);

        result.Skipped.Should().BeTrue();
        result.Sent.Should().BeFalse();
        result.Reason.Should().Be("No recipients were resolved for this email event.");
        dbContext.EmailDispatchLogs.Single(log => log.EventKey == EmailEventKeys.CustomerOrderConfirmed)
            .ToRecipientsJson.Should().Be("[]");
        emailServiceMock.Verify(
            service => service.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOverviewAsync_ShouldPreserveCustomizedRuleSettingsAcrossSeedSync()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var overview = await service.GetOverviewAsync();
        var customerRule = overview.Rules.Single(rule => rule.Id == "customer-order-confirmed");
        var updatedRule = customerRule with
        {
            Route = customerRule.Route with
            {
                StaticCc = ["custom.audit@zadana.local"]
            },
            Template = customerRule.Template with
            {
                Subject = new Dictionary<string, string>(customerRule.Template.Subject, StringComparer.OrdinalIgnoreCase)
                {
                    ["en"] = "Customized subject"
                }
            }
        };

        await service.UpdateRuleAsync(updatedRule.Id, updatedRule);

        var refreshed = await service.GetOverviewAsync();
        var refreshedRule = refreshed.Rules.Single(rule => rule.Id == updatedRule.Id);

        refreshedRule.Route.StaticCc.Should().Contain("custom.audit@zadana.local");
        refreshedRule.Template.Subject["en"].Should().Be("Customized subject");
    }

    [Fact]
    public async Task DispatchSystemEventEmailAsync_ShouldSupportLegacyEditedCustomerRuleWithoutBackfillingTargets()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User(
            fullName: "Legacy Customer",
            email: "legacy.customer@zadana.local",
            phone: "01055550003",
            role: UserRole.Customer);
        dbContext.Users.Add(customer);
        await dbContext.SaveChangesAsync();

        SendEmailRequest? capturedRequest = null;
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock
            .Setup(service => service.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new EmailSendResult("mock", true, "message-legacy", null));

        var service = CreateService(dbContext, emailServiceMock.Object);
        var overview = await service.GetOverviewAsync();
        var customerRule = overview.Rules.Single(rule => rule.Id == "customer-order-confirmed");
        var legacyEditedRule = customerRule with
        {
            RecipientTargets = new EmailRecipientTargetSelectionDto([], [], []),
            Template = customerRule.Template with
            {
                Subject = new Dictionary<string, string>(customerRule.Template.Subject, StringComparer.OrdinalIgnoreCase)
                {
                    ["en"] = "Legacy edited subject"
                }
            }
        };

        await service.UpdateRuleAsync(legacyEditedRule.Id, legacyEditedRule);

        var refreshed = await service.GetOverviewAsync();
        var refreshedRule = refreshed.Rules.Single(rule => rule.Id == legacyEditedRule.Id);

        refreshedRule.RecipientTargets.To.Should().BeEmpty();
        refreshedRule.Template.Subject["en"].Should().Be("Legacy edited subject");

        var result = await service.DispatchSystemEventEmailAsync(
            new EmailSystemEventDispatchRequest(
                EventKey: EmailEventKeys.CustomerOrderConfirmed,
                AudienceType: "customers",
                To: [customer.Email!],
                Variables: new Dictionary<string, string>
                {
                    ["order_number"] = "ORD-4001"
                },
                EntityId: Guid.NewGuid(),
                RecipientEntityId: customer.Id),
            CancellationToken.None);

        result.Sent.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.To.Should().Contain(customer.Email);
    }

    [Fact]
    public async Task ResolveRecipientsAndTestSend_ShouldSupportLegacyEditedScopedCustomerRule()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User(
            fullName: "Scoped Legacy Customer",
            email: "scoped.legacy@zadana.local",
            phone: "01055550004",
            role: UserRole.Customer);
        dbContext.Users.Add(customer);
        await dbContext.SaveChangesAsync();

        SendEmailRequest? capturedRequest = null;
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock
            .Setup(service => service.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new EmailSendResult("mock", true, "message-preview", null));

        var service = CreateService(dbContext, emailServiceMock.Object);
        var overview = await service.GetOverviewAsync();
        var customerRule = overview.Rules.Single(rule => rule.Id == "customer-order-confirmed");
        var scopedLegacyRule = customerRule with
        {
            EntityScope = customerRule.EntityScope with
            {
                EntityId = customer.Id.ToString()
            },
            RecipientTargets = new EmailRecipientTargetSelectionDto([], [], [])
        };

        await service.UpdateRuleAsync(scopedLegacyRule.Id, scopedLegacyRule);

        var resolved = await service.ResolveRecipientsAsync(scopedLegacyRule.Id, scopedLegacyRule);
        resolved.To.Should().Contain(customer.Email);
        resolved.Warnings.Should().BeEmpty();

        var result = await service.TestSendAsync(scopedLegacyRule.Id, scopedLegacyRule);
        result.Status.Should().Be("sent");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.To.Should().Contain(customer.Email);
    }

    private static EmailCenterService CreateService(
        ApplicationDbContext dbContext,
        IEmailService? emailService = null)
    {
        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(service => service.UserId).Returns(Guid.NewGuid());
        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);

        return new EmailCenterService(
            dbContext,
            emailService ?? Mock.Of<IEmailService>(service =>
                service.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()) ==
                Task.FromResult(new EmailSendResult("mock", true, "message-1", null))),
            currentUserService.Object);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"email-center-tests-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
