using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Infrastructure.Services;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Infrastructure;

public class WapilotWhatsAppOtpServiceTests
{
    [Fact]
    public async Task SendOtpSmsAsync_SendsExpectedTokenHeaderAndPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new CapturingHandler(async (request, _) =>
        {
            capturedRequest = request;
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = CreateService(handler, apiKey: "test-api-key");

        await service.SendOtpSmsAsync("01012345678", "1234");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.PathAndQuery.Should().Be("/api/v2/instance4218/send-message");
        capturedRequest.Headers.GetValues("token").Should().ContainSingle("test-api-key");
        capturedBody.Should().NotBeNullOrWhiteSpace();

        var form = ParseForm(capturedBody!);
        form["chat_id"].Should().Be("201012345678@c.us");
        form["text"].Should().Contain("1234");
        form["text"].Should().Contain("ZADANA verification code");
        form["text"].Should().Contain("```1234```");
        capturedBody.Should().NotContain("test-api-key");
    }

    [Fact]
    public async Task SendOtpSmsAsync_WhenApiKeyInvalid_ThrowsExternalServiceException()
    {
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var service = CreateService(handler, apiKey: "invalid-key");

        var act = () => service.SendOtpSmsAsync("+201012345678", "1234");

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.ErrorCode.Should().Be("WAPILOT_INVALID_API_KEY");
    }

    [Fact]
    public async Task SendOtpSmsAsync_WhenInstanceSuspended_ThrowsExternalServiceException()
    {
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var service = CreateService(handler, apiKey: "test-api-key");

        var act = () => service.SendOtpSmsAsync("+201012345678", "1234");

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.ErrorCode.Should().Be("WAPILOT_INSTANCE_SUSPENDED");
    }

    [Fact]
    public async Task SendOtpSmsAsync_WhenTimedOut_ThrowsExternalServiceException()
    {
        var handler = new CapturingHandler((_, _) => throw new TaskCanceledException("timeout"));
        var service = CreateService(handler, apiKey: "test-api-key");

        var act = () => service.SendOtpSmsAsync("+201012345678", "1234");

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.ErrorCode.Should().Be("WAPILOT_WHATSAPP_OTP_TIMEOUT");
    }

    [Fact]
    public async Task SendOtpSmsAsync_WhenPhoneInvalid_ThrowsBadRequestException()
    {
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var service = CreateService(handler, apiKey: "test-api-key");

        var act = () => service.SendOtpSmsAsync("not-a-phone", "1234");

        var ex = await act.Should().ThrowAsync<BadRequestException>();
        ex.Which.ErrorCode.Should().Be("INVALID_WHATSAPP_PHONE_NUMBER");
    }

    private static WapilotWhatsAppOtpService CreateService(HttpMessageHandler handler, string apiKey)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.wapilot.net")
        };

        var settings = Options.Create(new WapilotOtpSettings
        {
            Enabled = true,
            BaseUrl = "https://api.wapilot.net",
            SendMessagePath = "/api/v2/{instance_id}/send-message",
            InstanceId = "instance4218",
            ApiKey = apiKey,
            DefaultCountryCode = "+20",
            MessageTemplateEn = "ZADANA verification code:\n```{0}```\n\nDo not share this code with anyone."
        });

        return new WapilotWhatsAppOtpService(
            client,
            settings,
            NullLogger<WapilotWhatsAppOtpService>.Instance);
    }

    private static Dictionary<string, string> ParseForm(string encoded)
    {
        return encoded
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => Uri.UnescapeDataString(part[0]).Replace("+", " ", StringComparison.Ordinal),
                part => part.Length > 1
                    ? Uri.UnescapeDataString(part[1]).Replace("+", " ", StringComparison.Ordinal)
                    : string.Empty);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public CapturingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
