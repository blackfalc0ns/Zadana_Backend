using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
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
