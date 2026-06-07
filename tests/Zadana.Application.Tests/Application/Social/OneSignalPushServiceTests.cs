using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
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
            "Arabic title",
            "Title",
            "Arabic body",
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
        headings.GetProperty("ar").GetString().Should().Be("Arabic title");
        contents.GetProperty("en").GetString().Should().Be("Body");
        contents.GetProperty("ar").GetString().Should().Be("Arabic body");
        Guid.Parse(data.GetProperty("notificationId").GetString()!).Should().NotBeEmpty();
        data.GetProperty("type").GetString().Should().Be("order_status_changed");
        data.GetProperty("referenceId").GetGuid().Should().NotBeEmpty();
        data.GetProperty("orderId").GetString().Should().Be("123");
        data.GetProperty("click_action").GetString().Should().Be("FLUTTER_NOTIFICATION_CLICK");
    }

    [Fact]
    public async Task SendToExternalUserAsync_WithMobileOrderUpdatesProfile_ShouldBuildDisplayableMobilePayload()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"push-2"}""");
        var service = CreateService(handler);

        var result = await service.SendToExternalUserAsync(
            "customer-2",
            "Arabic title",
            "Title",
            "Arabic body",
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
        root.GetProperty("include_aliases").GetProperty("external_id")[0].GetString().Should().Be("customer-2");
        headings.GetProperty("en").GetString().Should().Be("Title");
        headings.GetProperty("ar").GetString().Should().Be("Arabic title");
        contents.GetProperty("en").GetString().Should().Be("Body");
        contents.GetProperty("ar").GetString().Should().Be("Arabic body");
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
            "Arabic title",
            "Title",
            "Arabic body",
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
        headings.GetProperty("ar").GetString().Should().Be("Arabic title");
        contents.GetProperty("en").GetString().Should().Be("Body");
        contents.GetProperty("ar").GetString().Should().Be("Arabic body");
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
            "Arabic title",
            "Title",
            "Arabic body",
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
    public async Task SendToExternalUserAsync_WithAdminTestAndOrderStatusHeadsUpPayloads_ShouldKeepEquivalentMobileEnvelopeContract()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            """{"id":"push-admin"}""",
            HttpStatusCode.OK,
            """{"id":"push-order"}""");
        var service = CreateService(handler);

        await service.SendToExternalUserAsync(
            "customer-admin",
            "Admin title ar",
            "Customer test notification",
            "Admin body ar",
            "This is a test notification sent from the admin API to verify customer mobile delivery.",
            "customer_test",
            null,
            """{"source":"admin_customer_notifications_test_api","customerId":"123","userId":"123","targetUrl":"/notifications"}""",
            "/notifications",
            OneSignalPushProfile.MobileHeadsUp,
            CancellationToken.None);

        await service.SendToExternalUserAsync(
            "customer-order",
            "Order title ar",
            "Order Accepted",
            "Order body ar",
            "Your order #ORD-123 has been accepted by the vendor",
            "order_status_changed",
            Guid.NewGuid(),
            """{"orderId":"456","orderNumber":"ORD-123","vendorId":"vendor-1","oldStatus":"PendingVendorAcceptance","newStatus":"Accepted","actorRole":"vendor","action":"status_changed","targetUrl":"/orders/456"}""",
            "/orders/456",
            OneSignalPushProfile.MobileHeadsUp,
            CancellationToken.None);

        handler.RequestBodies.Should().HaveCount(2);

        using var adminDocument = JsonDocument.Parse(handler.RequestBodies[0]);
        using var orderDocument = JsonDocument.Parse(handler.RequestBodies[1]);

        AssertEquivalentMobileEnvelope(adminDocument.RootElement, orderDocument.RootElement);
    }

    [Fact]
    public async Task SendToExternalUserAsync_ShouldLogUnifiedDiagnosticsOnSuccessAndFailure()
    {
        var logger = new RecordingLogger<OneSignalPushService>();
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            """{"id":"push-success","recipients":1}""",
            HttpStatusCode.BadRequest,
            """{"errors":["invalid_aliases"]}""");
        var service = CreateService(handler, logger);
        var failureReferenceId = Guid.NewGuid();

        await service.SendToExternalUserAsync(
            "customer-success",
            "Admin title ar",
            "Success title",
            "Admin body ar",
            "Success body",
            "customer_test",
            null,
            """{"source":"admin_customer_notifications_test_api","targetUrl":"/notifications"}""",
            "/notifications",
            OneSignalPushProfile.MobileHeadsUp,
            CancellationToken.None);

        var failedResult = await service.SendToExternalUserAsync(
            "customer-failure",
            "Order title ar",
            "Failure title",
            "Order body ar",
            "Failure body",
            "order_status_changed",
            failureReferenceId,
            """{"orderId":"456","targetUrl":"/orders/456"}""",
            "/orders/456",
            OneSignalPushProfile.MobileHeadsUp,
            CancellationToken.None);

        failedResult.Sent.Should().BeFalse();
        failedResult.ProviderStatusCode.Should().Be(400);
        failedResult.Reason.Should().Be("""{"errors":["invalid_aliases"]}""");

        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("customer-success", StringComparison.Ordinal) &&
            entry.Message.Contains("MobileHeadsUp", StringComparison.Ordinal) &&
            entry.Message.Contains("NotificationEventId", StringComparison.Ordinal) &&
            entry.Message.Contains("existing_android_channel_id:zadana_heads_up_notifications", StringComparison.Ordinal) &&
            entry.Message.Contains("click_action", StringComparison.Ordinal) &&
            entry.Message.Contains("push-success", StringComparison.Ordinal) &&
            entry.Message.Contains("\"id\":\"push-success\"", StringComparison.Ordinal));

        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("customer-failure", StringComparison.Ordinal) &&
            entry.Message.Contains("order_status_changed", StringComparison.Ordinal) &&
            entry.Message.Contains(failureReferenceId.ToString(), StringComparison.Ordinal) &&
            entry.Message.Contains("400", StringComparison.Ordinal) &&
            entry.Message.Contains("errors", StringComparison.Ordinal) &&
            entry.Message.Contains("invalid_aliases", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendToExternalUserAsync_WithSuccessfulNonJsonResponse_ShouldStillReturnSentResult()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "accepted");
        var service = CreateService(handler);

        var result = await service.SendToExternalUserAsync(
            "customer-plain-text",
            "Admin title ar",
            "Title",
            "Admin body ar",
            "Body",
            "customer_test",
            null,
            """{"source":"admin_customer_notifications_test_api"}""",
            "/notifications",
            OneSignalPushProfile.MobileHeadsUp,
            CancellationToken.None);

        result.Sent.Should().BeTrue();
        result.ProviderNotificationId.Should().BeNull();
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
            "Arabic title",
            "Title",
            "Arabic body",
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

    [Fact]
    public async Task SendToExternalUserAsync_WithPlaceholderCredentials_ShouldSkipAsMissingConfiguration()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(handler, configureSettings: settings =>
        {
            settings.AppId = "__SET_VIA_ENV__OneSignal__AppId";
            settings.RestApiKey = "PUT_ONESIGNAL_REST_API_KEY_HERE";
        });

        var result = await service.SendToExternalUserAsync(
            "customer-missing-config",
            "Arabic title",
            "Title",
            "Arabic body",
            "Body",
            "customer_test",
            null,
            null,
            null,
            CancellationToken.None);

        result.Skipped.Should().BeTrue();
        result.Attempted.Should().BeFalse();
        result.Reason.Should().Contain("Customer OneSignal AppId or RestApiKey is not configured");
        handler.RequestBodies.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToExternalUsersAsync_WithDriverTargetAndMissingCredentials_ShouldReturnDriverSpecificReason()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(handler, configureSettings: settings =>
        {
            settings.AppId = string.Empty;
            settings.RestApiKey = string.Empty;
            settings.DriverAppId = string.Empty;
            settings.DriverRestApiKey = string.Empty;
        });

        var results = await service.SendToExternalUsersAsync(
            ["driver-missing-config"],
            "Arabic title",
            "Title",
            "Arabic body",
            "Body",
            "driver_test",
            null,
            null,
            "/notifications",
            OneSignalPushProfile.MobileHeadsUp,
            OneSignalApplicationTarget.Driver,
            CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Skipped.Should().BeTrue();
        results[0].Attempted.Should().BeFalse();
        results[0].Reason.Should().Contain("Driver OneSignal AppId or RestApiKey is not configured");
        results[0].Reason.Should().Contain("OneSignal__DriverAppId");
        handler.RequestBodies.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToExternalUsersAsync_WithDriverTargetAndUserId_ShouldSendUserIdAlias()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.Parse("1363aa39-64cb-42a8-9466-d2415f247c09");
        var driver = new Driver(
            userId,
            vehicleType: null,
            nationalId: null,
            licenseNumber: null);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"driver-push"}""");
        var service = CreateService(
            handler,
            scopeFactory: new DbContextServiceScopeFactory(dbContext),
            configureSettings: settings =>
            {
                settings.DriverAppId = "driver-app-id";
                settings.DriverRestApiKey = "driver-rest-key";
            });

        var results = await service.SendToExternalUsersAsync(
            [userId.ToString()],
            "عنوان",
            "Driver title",
            "نص",
            "Driver body",
            "driver_test",
            null,
            null,
            "/notifications",
            OneSignalPushProfile.MobileHeadsUp,
            OneSignalApplicationTarget.Driver,
            CancellationToken.None);

        results.Should().ContainSingle(result => result.Sent);
        handler.RequestBodies.Should().HaveCount(1);

        using var document = JsonDocument.Parse(handler.RequestBodies[0]);
        var externalIds = document.RootElement
            .GetProperty("include_aliases")
            .GetProperty("external_id")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        externalIds.Should().Equal(userId.ToString());
        externalIds.Should().NotContain(driver.Id.ToString());
        document.RootElement.GetProperty("app_id").GetString().Should().Be("driver-app-id");
    }

    [Fact]
    public async Task SendToExternalUsersAsync_WithDriverTargetAndDriverId_ShouldNormalizeToUserIdAlias()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var driver = new Driver(
            userId,
            vehicleType: null,
            nationalId: null,
            licenseNumber: null);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"driver-push"}""");
        var service = CreateService(
            handler,
            scopeFactory: new DbContextServiceScopeFactory(dbContext),
            configureSettings: settings =>
            {
                settings.DriverAppId = "driver-app-id";
                settings.DriverRestApiKey = "driver-rest-key";
            });

        await service.SendToExternalUsersAsync(
            [driver.Id.ToString()],
            "عنوان",
            "Driver title",
            "نص",
            "Driver body",
            "driver_test",
            null,
            null,
            "/notifications",
            OneSignalPushProfile.MobileHeadsUp,
            OneSignalApplicationTarget.Driver,
            CancellationToken.None);

        using var document = JsonDocument.Parse(handler.RequestBodies.Single());
        document.RootElement
            .GetProperty("include_aliases")
            .GetProperty("external_id")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Should()
            .Equal(userId.ToString());
    }

    [Fact]
    public async Task SendToExternalUsersAsync_WithDriverTargetRecipientErrors_ShouldFallbackToProviderSubscriptionIds()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.Parse("1363aa39-64cb-42a8-9466-d2415f247c09");
        var driver = new Driver(
            userId,
            vehicleType: null,
            nationalId: null,
            licenseNumber: null);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var enabledSubscriptionId = Guid.Parse("f783fa4e-fd24-4258-824d-22996bc8681f");
        var aliasErrorBody = JsonSerializer.Serialize(new
        {
            id = "alias-push",
            errors = new
            {
                invalid_aliases = new Dictionary<string, string[]>
                {
                    ["external_id"] = [userId.ToString()]
                }
            }
        });
        var providerUserBody = JsonSerializer.Serialize(new
        {
            identity = new
            {
                external_id = userId.ToString(),
                onesignal_id = "dee28e12-3906-43bd-b49d-c4da7aa3784b"
            },
            subscriptions = new[]
            {
                new { id = enabledSubscriptionId.ToString(), enabled = true, type = "AndroidPush" },
                new { id = "bdc466d9-d487-491c-96a5-3a04ba215489", enabled = false, type = "AndroidPush" }
            }
        });
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            aliasErrorBody,
            HttpStatusCode.OK,
            providerUserBody,
            HttpStatusCode.OK,
            """{"id":"fallback-push"}""");
        var service = CreateService(
            handler,
            scopeFactory: new DbContextServiceScopeFactory(dbContext),
            configureSettings: settings =>
            {
                settings.DriverAppId = "driver-app-id";
                settings.DriverRestApiKey = "driver-rest-key";
            });

        var results = await service.SendToExternalUsersAsync(
            [userId.ToString()],
            "عنوان",
            "Driver title",
            "نص",
            "Driver body",
            "driver_test",
            null,
            null,
            "/notifications",
            OneSignalPushProfile.MobileHeadsUp,
            OneSignalApplicationTarget.Driver,
            CancellationToken.None);

        results.Should().ContainSingle(result =>
            result.Sent &&
            result.ProviderNotificationId == "fallback-push");
        handler.RequestMethods.Should().Equal("POST", "GET", "POST");
        handler.RequestUris[1].Should().Contain(userId.ToString());

        var firstPostBody = handler.RequestBodies[0];
        using var firstPost = JsonDocument.Parse(firstPostBody);
        firstPost.RootElement
            .GetProperty("include_aliases")
            .GetProperty("external_id")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Should()
            .Equal(userId.ToString());

        var fallbackPostBody = handler.RequestBodies[2];
        using var fallbackPost = JsonDocument.Parse(fallbackPostBody);
        fallbackPost.RootElement
            .GetProperty("include_subscription_ids")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Should()
            .Equal(enabledSubscriptionId.ToString());
    }

    [Theory]
    [InlineData("2c10f9d2-35f4-4c0d-8e44-38f10f0dced1", true)]
    [InlineData(" 2c10f9d2-35f4-4c0d-8e44-38f10f0dced1 ", true)]
    [InlineData("dc1VfjmgQBiTYWTnHuc1qL:APA91bHWB4lIeFx8SoSaJnsYsuWqoxc8dh7QD34MDrZ2NCtUylakoUvbnP_GPq6ErI77wjQFYJU6TenycA8JsvJ9jcc9o8pASFvEG8hMUWuUTH4K0U22QMQ", false)]
    [InlineData("os_v2_app_d3vnd2r5n5hsvc6fy2a5og2v6ylj2wihmmjuoierhp6pa22xk6de2xtadq5apxj3rgki7ojhdhm5vmhysn6v4yhpobo64cr4ovffcpa", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidOneSignalSubscriptionId_ShouldOnlyAcceptUuidValues(string? value, bool expected)
    {
        OneSignalPushService.IsValidOneSignalSubscriptionId(value).Should().Be(expected);
    }

    [Fact]
    public async Task SendToExternalUsersAsync_WithAdminWebTarget_ShouldPreferRegisteredSubscriptionIdsBeforeAliases()
    {
        await using var dbContext = CreateDbContext();
        var adminUserId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var subscriptionId = Guid.Parse("2c10f9d2-35f4-4c0d-8e44-38f10f0dced1");
        dbContext.UserPushDevices.Add(new UserPushDevice(
            adminUserId,
            subscriptionId.ToString(),
            PushPlatform.Web,
            deviceId: $"admin-web-{adminUserId}",
            deviceName: "Edge",
            appVersion: "superadmin-panel",
            locale: "ar",
            notificationsEnabled: true));
        await dbContext.SaveChangesAsync();

        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"admin-subscription-push"}""");
        var service = CreateService(
            handler,
            scopeFactory: new DbContextServiceScopeFactory(dbContext),
            configureSettings: settings =>
            {
                settings.AdminWebAppId = "admin-web-app-id";
                settings.AdminWebRestApiKey = "admin-web-rest-key";
            });

        var results = await service.SendToExternalUsersAsync(
            [adminUserId.ToString()],
            "تنبيه",
            "Alert",
            "نص",
            "Body",
            AdminAlertTypes.DeliveryDispatchStuck,
            Guid.NewGuid(),
            null,
            "/notifications",
            OneSignalPushProfile.Default,
            OneSignalApplicationTarget.AdminWeb,
            CancellationToken.None);

        results.Should().ContainSingle(result => result.Sent && result.ProviderNotificationId == "admin-subscription-push");
        handler.RequestBodies.Should().HaveCount(1);

        using var document = JsonDocument.Parse(handler.RequestBodies[0]);
        document.RootElement
            .GetProperty("include_subscription_ids")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Should()
            .Equal(subscriptionId.ToString());
    }

    [Fact]
    public async Task SendToExternalUsersAsync_WithAdminWebAndArabicDeviceLocale_ShouldPinArabicIntoEnglishFallbackSlot()
    {
        await using var dbContext = CreateDbContext();
        var adminUserId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var subscriptionId = Guid.Parse("2c10f9d2-35f4-4c0d-8e44-38f10f0dced1");
        dbContext.UserPushDevices.Add(new UserPushDevice(
            adminUserId,
            subscriptionId.ToString(),
            PushPlatform.Web,
            deviceId: $"admin-web-{adminUserId}",
            deviceName: "Edge",
            appVersion: "superadmin-panel",
            locale: "ar",
            notificationsEnabled: true));
        await dbContext.SaveChangesAsync();

        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"admin-ar-push"}""");
        var service = CreateService(
            handler,
            scopeFactory: new DbContextServiceScopeFactory(dbContext),
            configureSettings: settings =>
            {
                settings.AdminWebAppId = "admin-web-app-id";
                settings.AdminWebRestApiKey = "admin-web-rest-key";
            });

        var results = await service.SendToExternalUsersAsync(
            [adminUserId.ToString()],
            "طلب بدون مندوب",
            "Order without driver",
            "الطلب ينتظر مندوب توصيل.",
            "Order is waiting for a driver.",
            AdminAlertTypes.DeliveryDispatchStuck,
            Guid.NewGuid(),
            null,
            "/notifications",
            OneSignalPushProfile.Default,
            OneSignalApplicationTarget.AdminWeb,
            CancellationToken.None);

        results.Should().ContainSingle(result => result.Sent);
        handler.RequestBodies.Should().HaveCount(1);

        using var document = JsonDocument.Parse(handler.RequestBodies[0]);
        var headings = document.RootElement.GetProperty("headings");
        var contents = document.RootElement.GetProperty("contents");

        headings.GetProperty("en").GetString().Should().Be("طلب بدون مندوب");
        headings.GetProperty("ar").GetString().Should().Be("طلب بدون مندوب");
        contents.GetProperty("en").GetString().Should().Be("الطلب ينتظر مندوب توصيل.");
        contents.GetProperty("ar").GetString().Should().Be("الطلب ينتظر مندوب توصيل.");
    }

    private static OneSignalPushService CreateService(
        RecordingHttpMessageHandler handler,
        ILogger<OneSignalPushService>? logger = null,
        Action<OneSignalSettings>? configureSettings = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.onesignal.com/")
        };

        var settings = new OneSignalSettings
        {
            Enabled = true,
            AppId = "app-id",
            RestApiKey = "rest-key",
            UseEnvironmentVariableFallback = false,
            BaseUrl = "https://api.onesignal.com",
            DefaultWebUrl = "https://vendor.example/",
            MobileHeadsUpAndroidChannelId = "zadana_heads_up_notifications",
            MobileHeadsUpExistingAndroidChannelId = "zadana_heads_up_notifications",
            MobileHeadsUpPriority = 10,
            OrderUpdatesAndroidChannelId = "zadana_order_updates_realtime_v2",
            OrderUpdatesExistingAndroidChannelId = "zadana_order_updates_realtime_v2",
            OrderUpdatesPriority = 10
        };
        configureSettings?.Invoke(settings);

        return new OneSignalPushService(
            httpClient,
            Options.Create(settings),
            logger ?? NullLogger<OneSignalPushService>.Instance,
            scopeFactory ?? new NoOpServiceScopeFactory());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private sealed class NoOpServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new NoOpServiceScope();
    }

    private sealed class NoOpServiceScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();

        public void Dispose()
        {
        }
    }

    private sealed class DbContextServiceScopeFactory(ApplicationDbContext dbContext) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new DbContextServiceScope(dbContext);
    }

    private sealed class DbContextServiceScope(ApplicationDbContext dbContext) : IServiceScope, IServiceProvider
    {
        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(ApplicationDbContext)
                ? dbContext
                : null;

        public void Dispose()
        {
        }
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
        public List<string> RequestMethods { get; } = [];
        public List<string> RequestUris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestMethods.Add(request.Method.Method);
            RequestUris.Add(request.RequestUri?.ToString() ?? string.Empty);
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        public sealed record LogEntry(LogLevel Level, string Message);

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private static void AssertEquivalentMobileEnvelope(JsonElement expected, JsonElement actual)
    {
        AssertLocalizedContentShape(expected.GetProperty("headings"), actual.GetProperty("headings"));
        AssertLocalizedContentShape(expected.GetProperty("contents"), actual.GetProperty("contents"));

        GetChannelPropertyName(expected).Should().Be(GetChannelPropertyName(actual));
        GetChannelPropertyValue(expected).Should().Be(GetChannelPropertyValue(actual));

        actual.GetProperty("priority").GetInt32().Should().Be(expected.GetProperty("priority").GetInt32());
        actual.GetProperty("android_accent_color").GetString().Should().Be(expected.GetProperty("android_accent_color").GetString());
        actual.GetProperty("content_available").GetBoolean().Should().Be(expected.GetProperty("content_available").GetBoolean());
        actual.GetProperty("mutable_content").GetBoolean().Should().Be(expected.GetProperty("mutable_content").GetBoolean());
        actual.GetProperty("isAndroid").GetBoolean().Should().Be(expected.GetProperty("isAndroid").GetBoolean());
        actual.GetProperty("isIos").GetBoolean().Should().Be(expected.GetProperty("isIos").GetBoolean());
        actual.GetProperty("isAnyWeb").GetBoolean().Should().Be(expected.GetProperty("isAnyWeb").GetBoolean());
        actual.GetProperty("data").GetProperty("click_action").GetString().Should().Be(
            expected.GetProperty("data").GetProperty("click_action").GetString());
    }

    private static void AssertLocalizedContentShape(JsonElement expected, JsonElement actual)
    {
        expected.EnumerateObject().Select(property => property.Name).Should().Equal(
            actual.EnumerateObject().Select(property => property.Name));
    }

    private static string GetChannelPropertyName(JsonElement root)
    {
        if (root.TryGetProperty("existing_android_channel_id", out _))
        {
            return "existing_android_channel_id";
        }

        if (root.TryGetProperty("android_channel_id", out _))
        {
            return "android_channel_id";
        }

        return "none";
    }

    private static string? GetChannelPropertyValue(JsonElement root)
    {
        if (root.TryGetProperty("existing_android_channel_id", out var existingChannel))
        {
            return existingChannel.GetString();
        }

        if (root.TryGetProperty("android_channel_id", out var androidChannel))
        {
            return androidChannel.GetString();
        }

        return null;
    }
}
