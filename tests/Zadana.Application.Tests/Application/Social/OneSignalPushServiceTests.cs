using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Services;
using Zadana.Infrastructure.Settings;

namespace Zadana.Application.Tests.Application.Social;

public class OneSignalPushServiceTests
{
    [Fact]
    public async Task SendToExternalUserAsync_WithMobileHeadsUpProfile_ShouldBuildDisplayableMobilePayload()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"push-1"}""");
        var service = CreateService(handler);

        var result = await service.SendToExternalUserAsync(
            "customer-1",
            "عنوان",
            "Title",
            "محتوى",
            "Body",
            "order_status_changed",
            Guid.NewGuid(),
            """{"orderId":"123"}""",
            "/orders/123",
            OneSignalPushProfile.MobileHeadsUp,
            CancellationToken.None);

        result.Sent.Should().BeTrue();
        handler.RequestBodies.Should().HaveCount(1);

        using var document = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = document.RootElement;
        var data = root.GetProperty("data");
        var headings = root.GetProperty("headings");
        var contents = root.GetProperty("contents");

        root.GetProperty("existing_android_channel_id").GetString().Should().Be("zadana_heads_up_notifications");
        root.TryGetProperty("android_channel_id", out _).Should().BeFalse();
        Guid.Parse(root.GetProperty("collapse_id").GetString()!).Should().NotBeEmpty();
        Guid.Parse(root.GetProperty("idempotency_key").GetString()!).Should().NotBeEmpty();
        root.GetProperty("priority").GetInt32().Should().Be(10);
        root.GetProperty("android_accent_color").GetString().Should().Be("FF127C8C");
        root.GetProperty("content_available").GetBoolean().Should().BeTrue();
        root.GetProperty("mutable_content").GetBoolean().Should().BeTrue();
        root.GetProperty("isAndroid").GetBoolean().Should().BeTrue();
        root.GetProperty("isIos").GetBoolean().Should().BeTrue();
        root.GetProperty("isAnyWeb").GetBoolean().Should().BeFalse();
        root.TryGetProperty("web_url", out _).Should().BeFalse();
        root.GetProperty("include_aliases").GetProperty("external_id")[0].GetString().Should().Be("customer-1");
        headings.GetProperty("en").GetString().Should().Be("Title");
        headings.GetProperty("ar").GetString().Should().Be("Ø¹Ù†ÙˆØ§Ù†");
        contents.GetProperty("en").GetString().Should().Be("Body");
        contents.GetProperty("ar").GetString().Should().Be("Ù…Ø­ØªÙˆÙ‰");
        Guid.Parse(data.GetProperty("notificationId").GetString()!).Should().NotBeEmpty();
        data.GetProperty("type").GetString().Should().Be("order_status_changed");
        data.GetProperty("referenceId").GetGuid().Should().NotBeEmpty();
        data.GetProperty("orderId").GetString().Should().Be("123");
        data.GetProperty("click_action").GetString().Should().Be("FLUTTER_NOTIFICATION_CLICK");
    }

    [Fact]
    public async Task SendToExternalUserAsync_WithMobileOrderUpdatesProfile_ShouldUseOrderUpdatesChannelFields()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"push-2"}""");
        var service = CreateService(handler);

        var result = await service.SendToExternalUserAsync(
            "customer-2",
            "عنوان",
            "Title",
            "محتوى",
            "Body",
            "order_status_changed",
            Guid.NewGuid(),
            """{"orderId":"456"}""",
            "/orders/456",
            OneSignalPushProfile.MobileOrderUpdates,
            CancellationToken.None);

        result.Sent.Should().BeTrue();
        handler.RequestBodies.Should().HaveCount(1);

        using var document = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = document.RootElement;
        var data = root.GetProperty("data");
        var headings = root.GetProperty("headings");
        var contents = root.GetProperty("contents");

        root.GetProperty("existing_android_channel_id").GetString().Should().Be("zadana_order_updates_realtime_v2");
        root.TryGetProperty("android_channel_id", out _).Should().BeFalse();
        Guid.Parse(root.GetProperty("collapse_id").GetString()!).Should().NotBeEmpty();
        Guid.Parse(root.GetProperty("idempotency_key").GetString()!).Should().NotBeEmpty();
        root.GetProperty("priority").GetInt32().Should().Be(10);
        root.GetProperty("android_accent_color").GetString().Should().Be("FF127C8C");
        root.GetProperty("content_available").GetBoolean().Should().BeTrue();
        root.GetProperty("mutable_content").GetBoolean().Should().BeTrue();
        root.GetProperty("isAndroid").GetBoolean().Should().BeTrue();
        root.GetProperty("isIos").GetBoolean().Should().BeTrue();
        root.GetProperty("isAnyWeb").GetBoolean().Should().BeFalse();
        root.TryGetProperty("web_url", out _).Should().BeFalse();
        root.GetProperty("include_aliases").GetProperty("external_id")[0].GetString().Should().Be("customer-2");
        headings.GetProperty("en").GetString().Should().Be("Title");
        headings.GetProperty("ar").GetString().Should().Be("Ø¹Ù†ÙˆØ§Ù†");
        contents.GetProperty("en").GetString().Should().Be("Body");
        contents.GetProperty("ar").GetString().Should().Be("Ù…Ø­ØªÙˆÙ‰");
        Guid.Parse(data.GetProperty("notificationId").GetString()!).Should().NotBeEmpty();
        data.GetProperty("type").GetString().Should().Be("order_status_changed");
        data.GetProperty("referenceId").GetGuid().Should().NotBeEmpty();
        data.GetProperty("orderId").GetString().Should().Be("456");
        data.GetProperty("click_action").GetString().Should().Be("FLUTTER_NOTIFICATION_CLICK");
    }

    [Fact]
    public async Task SendToExternalUserAsync_WithDefaultProfile_ShouldKeepExistingPayloadShape()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"push-1"}""");
        var service = CreateService(handler);

        var result = await service.SendToExternalUserAsync(
            "vendor-1",
            "عنوان",
            "Title",
            "محتوى",
            "Body",
            "vendor_test",
            Guid.NewGuid(),
            """{"vendorId":"123"}""",
            "/orders/123",
            CancellationToken.None);

        result.Sent.Should().BeTrue();
        handler.RequestBodies.Should().HaveCount(1);

        using var document = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = document.RootElement;
        var headings = root.GetProperty("headings");
        var contents = root.GetProperty("contents");

        root.TryGetProperty("android_channel_id", out _).Should().BeFalse();
        root.TryGetProperty("priority", out _).Should().BeFalse();
        root.TryGetProperty("content_available", out _).Should().BeFalse();
        root.TryGetProperty("mutable_content", out _).Should().BeFalse();
        root.TryGetProperty("isAndroid", out _).Should().BeFalse();
        root.TryGetProperty("isIos", out _).Should().BeFalse();
        root.TryGetProperty("isAnyWeb", out _).Should().BeFalse();
        root.GetProperty("web_url").GetString().Should().Be("https://vendor.example/orders/123");
        root.GetProperty("include_aliases").GetProperty("external_id")[0].GetString().Should().Be("vendor-1");
        headings.GetProperty("en").GetString().Should().Be("Title");
        headings.GetProperty("ar").GetString().Should().Be("Ø¹Ù†ÙˆØ§Ù†");
        contents.GetProperty("en").GetString().Should().Be("Body");
        contents.GetProperty("ar").GetString().Should().Be("Ù…Ø­ØªÙˆÙ‰");
        Guid.Parse(root.GetProperty("collapse_id").GetString()!).Should().NotBeEmpty();
        Guid.Parse(root.GetProperty("idempotency_key").GetString()!).Should().NotBeEmpty();
        Guid.Parse(root.GetProperty("data").GetProperty("notificationId").GetString()!).Should().NotBeEmpty();
    }

    [Fact]
    public async Task SendToExternalUserAsync_WithMobileProfile_ShouldPreserveExistingClickAction()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"push-3"}""");
        var service = CreateService(handler);

        var result = await service.SendToExternalUserAsync(
            "customer-3",
            "Ø¹Ù†ÙˆØ§Ù†",
            "Title",
            "Ù…Ø­ØªÙˆÙ‰",
            "Body",
            "order_status_changed",
            Guid.NewGuid(),
            """{"orderId":"789","click_action":"CUSTOM_CLICK"}""",
            "/orders/789",
            OneSignalPushProfile.MobileOrderUpdates,
            CancellationToken.None);

        result.Sent.Should().BeTrue();
        handler.RequestBodies.Should().HaveCount(1);

        using var document = JsonDocument.Parse(handler.RequestBodies[0]);
        document.RootElement
            .GetProperty("data")
            .GetProperty("click_action")
            .GetString()
            .Should()
            .Be("CUSTOM_CLICK");
    }

    [Fact]
    public async Task SendToExternalUsersAsync_ShouldSplitRequestsIntoBatchesOfTwentyThousand()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            """{"id":"batch-1"}""",
            HttpStatusCode.OK,
            """{"id":"batch-2"}""");
        var service = CreateService(handler);
        var externalUserIds = Enumerable.Range(1, 20_001)
            .Select(index => $"customer-{index}")
            .ToArray();

        var results = await service.SendToExternalUsersAsync(
            externalUserIds,
            "عنوان",
            "Title",
            "محتوى",
            "Body",
            "new_banner",
            null,
            """{"bannerId":"123"}""",
            null,
            OneSignalPushProfile.MobileHeadsUp,
            CancellationToken.None);

        results.Should().HaveCount(2);
        handler.RequestBodies.Should().HaveCount(2);

        using var firstBatch = JsonDocument.Parse(handler.RequestBodies[0]);
        using var secondBatch = JsonDocument.Parse(handler.RequestBodies[1]);

        firstBatch.RootElement
            .GetProperty("include_aliases")
            .GetProperty("external_id")
            .GetArrayLength()
            .Should()
            .Be(20_000);
        secondBatch.RootElement
            .GetProperty("include_aliases")
            .GetProperty("external_id")
            .GetArrayLength()
            .Should()
            .Be(1);
    }

    private static OneSignalPushService CreateService(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.onesignal.com/")
        };

        return new OneSignalPushService(
            httpClient,
            Options.Create(new OneSignalSettings
            {
                Enabled = true,
                AppId = "app-id",
                RestApiKey = "rest-key",
                BaseUrl = "https://api.onesignal.com",
                DefaultWebUrl = "https://vendor.example/",
                MobileHeadsUpAndroidChannelId = "zadana_heads_up_notifications",
                MobileHeadsUpExistingAndroidChannelId = "zadana_heads_up_notifications",
                MobileHeadsUpPriority = 10,
                OrderUpdatesAndroidChannelId = string.Empty,
                OrderUpdatesExistingAndroidChannelId = "zadana_order_updates_realtime_v2",
                OrderUpdatesPriority = 10
            }),
            NullLogger<OneSignalPushService>.Instance);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public RecordingHttpMessageHandler(params object[] responses)
        {
            for (var i = 0; i < responses.Length; i += 2)
            {
                _responses.Enqueue(new HttpResponseMessage((HttpStatusCode)responses[i])
                {
                    Content = new StringContent((string)responses[i + 1], Encoding.UTF8, "application/json")
                });
            }
        }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}
