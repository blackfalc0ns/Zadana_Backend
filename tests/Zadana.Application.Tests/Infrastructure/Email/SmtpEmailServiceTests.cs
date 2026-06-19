using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Email;

namespace Zadana.Application.Tests.Infrastructure.Email;

public sealed class SmtpEmailServiceTests
{
    [Fact]
    public void BuildMessage_PreservesEmailContractAndUsesAllowedSender()
    {
        var service = CreateService();

        var message = service.BuildMessage(new SendEmailRequest(
            ["customer@example.com"],
            "Subject",
            "<strong>Hello</strong>",
            TextBody: "Hello",
            From: "Zadna Support <support@zadna0.com>",
            ReplyTo: "help@zadna0.com",
            Cc: ["cc@example.com"],
            Bcc: ["bcc@example.com"],
            Metadata: new Dictionary<string, string> { ["workflow"] = "otp" },
            Headers: new Dictionary<string, string>
            {
                ["X-Entity-Ref-ID"] = "otp-1",
                ["Subject"] = "must-not-override"
            }));

        message.From.Mailboxes.Single().Address.Should().Be("support@zadna0.com");
        message.To.Mailboxes.Single().Address.Should().Be("customer@example.com");
        message.Cc.Mailboxes.Single().Address.Should().Be("cc@example.com");
        message.Bcc.Mailboxes.Single().Address.Should().Be("bcc@example.com");
        message.ReplyTo.Mailboxes.Single().Address.Should().Be("help@zadna0.com");
        message.Subject.Should().Be("Subject");
        message.Headers["X-Zadana-workflow"].Should().Be("otp");
        message.Headers["X-Entity-Ref-ID"].Should().Be("otp-1");
        message.MessageId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildMessage_UnapprovedFromAddressFallsBackToConfiguredSender()
    {
        var service = CreateService();

        var message = service.BuildMessage(new SendEmailRequest(
            ["customer@example.com"],
            "Subject",
            "<p>Hello</p>",
            From: "Attacker <outside@example.net>"));

        message.From.Mailboxes.Single().Address.Should().Be("support@zadna0.com");
        message.From.Mailboxes.Single().Name.Should().Be("Zadna");
    }

    private static SmtpEmailService CreateService() =>
        new(
            Options.Create(new EmailSettings
            {
                FromEmail = "support@zadna0.com",
                FromName = "Zadna",
                SupportEmail = "support@zadna0.com",
                HelloEmail = "hello@zadna0.com",
                InfoEmail = "info@zadna0.com",
                ContactEmail = "contact@zadna0.com",
                LogoUrl = "https://media.zadna0.com/logo.webp",
                OtpHeroImageUrl = "https://media.zadna0.com/otp.webp",
                Smtp = new SmtpEmailSettings
                {
                    Host = "mail.zadna0.com",
                    Port = 587,
                    Security = "StartTls",
                    Username = "support@zadna0.com",
                    Password = "test-password"
                }
            }),
            NullLogger<SmtpEmailService>.Instance);
}
