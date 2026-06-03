using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Infrastructure.Services;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Infrastructure;

public class WhatsAppCloudOtpServiceTests
{
    [Fact]
    public async Task SendOtpSmsAsync_SendsAuthenticationTemplateWithCopyCodeButton()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new CapturingHandler(async (request, _) =>
        {
            capturedRequest = request;
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = CreateService(handler, accessToken: "test-cloud-token");

        await service.SendOtpSmsAsync("01012345678", "8809");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.PathAndQuery.Should().Be("/v23.0/123456789/messages");
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("test-cloud-token");
        capturedBody.Should().NotBeNullOrWhiteSpace();
        capturedBody.Should().NotContain("test-cloud-token");

        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        root.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        root.GetProperty("to").GetString().Should().Be("201012345678");
        root.GetProperty("type").GetString().Should().Be("template");

        var template = root.GetProperty("template");
        template.GetProperty("name").GetString().Should().Be("zadana_otp_copy_code");
        template.GetProperty("language").GetProperty("code").GetString().Should().Be("en_US");

        var components = template.GetProperty("components");
        components[0].GetProperty("type").GetString().Should().Be("body");
        components[0].GetProperty("parameters")[0].GetProperty("text").GetString().Should().Be("8809");

        components[1].GetProperty("type").GetString().Should().Be("button");
        components[1].GetProperty("sub_type").GetString().Should().Be("copy_code");
        components[1].GetProperty("index").GetString().Should().Be("0");
        components[1].GetProperty("parameters")[0].GetProperty("type").GetString().Should().Be("coupon_code");
        components[1].GetProperty("parameters")[0].GetProperty("coupon_code").GetString().Should().Be("8809");
    }

    [Fact]
    public async Task SendOtpSmsAsync_WhenAccessTokenInvalid_ThrowsExternalServiceException()
    {
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var service = CreateService(handler, accessToken: "invalid-token");

        var act = () => service.SendOtpSmsAsync("+201012345678", "1234");

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.ErrorCode.Should().Be("WHATSAPP_CLOUD_INVALID_ACCESS_TOKEN");
    }

    [Fact]
    public async Task SendOtpSmsAsync_WhenPhoneInvalid_ThrowsBadRequestException()
    {
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var service = CreateService(handler, accessToken: "test-cloud-token");

        var act = () => service.SendOtpSmsAsync("not-a-phone", "1234");

        var ex = await act.Should().ThrowAsync<BadRequestException>();
        ex.Which.ErrorCode.Should().Be("INVALID_WHATSAPP_PHONE_NUMBER");
    }

    private static WhatsAppCloudOtpService CreateService(HttpMessageHandler handler, string accessToken)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.facebook.com")
        };

        var settings = Options.Create(new WhatsAppCloudOtpSettings
        {
            Enabled = true,
            BaseUrl = "https://graph.facebook.com",
            GraphVersion = "v23.0",
            PhoneNumberId = "123456789",
            AccessToken = accessToken,
            DefaultCountryCode = "+20",
            TemplateName = "zadana_otp_copy_code",
            LanguageCode = "en_US",
            CopyCodeButtonIndex = 0
        });

        return new WhatsAppCloudOtpService(
            client,
            settings,
            NullLogger<WhatsAppCloudOtpService>.Instance);
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
