using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Infrastructure.Services;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Infrastructure;

public class NabdaWhatsAppOtpServiceTests
{
    [Fact]
    public async Task SendOtpSmsAsync_SendsExpectedAuthorizationHeaderAndPayload()
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
        capturedRequest.RequestUri!.PathAndQuery.Should().Be("/api/v1/messages/send");
        capturedRequest.Headers.GetValues("Authorization").Should().ContainSingle("test-api-key");
        capturedBody.Should().NotBeNullOrWhiteSpace();

        using var body = JsonDocument.Parse(capturedBody!);
        body.RootElement.GetProperty("phone").GetString().Should().Be("+201012345678");
        body.RootElement.GetProperty("message").GetString().Should().Contain("1234");
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
        ex.Which.ErrorCode.Should().Be("NABDA_INVALID_API_KEY");
    }

    [Fact]
    public async Task SendOtpSmsAsync_WhenInstanceSuspended_ThrowsExternalServiceException()
    {
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var service = CreateService(handler, apiKey: "test-api-key");

        var act = () => service.SendOtpSmsAsync("+201012345678", "1234");

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.ErrorCode.Should().Be("NABDA_INSTANCE_SUSPENDED");
    }

    [Fact]
    public async Task SendOtpSmsAsync_WhenTimedOut_ThrowsExternalServiceException()
    {
        var handler = new CapturingHandler((_, _) => throw new TaskCanceledException("timeout"));
        var service = CreateService(handler, apiKey: "test-api-key");

        var act = () => service.SendOtpSmsAsync("+201012345678", "1234");

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.ErrorCode.Should().Be("NABDA_WHATSAPP_OTP_TIMEOUT");
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

    private static NabdaWhatsAppOtpService CreateService(HttpMessageHandler handler, string apiKey)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.nabdaotp.com")
        };

        var settings = Options.Create(new NabdaOtpSettings
        {
            Enabled = true,
            BaseUrl = "https://api.nabdaotp.com",
            ApiKey = apiKey,
            DefaultCountryCode = "+20",
            MessageTemplateEn = "Your Zadana verification code is {0}. Do not share it with anyone."
        });

        return new NabdaWhatsAppOtpService(
            client,
            settings,
            NullLogger<NabdaWhatsAppOtpService>.Instance);
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
