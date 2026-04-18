using System.Text.Json;
using FluentAssertions;
using Zadana.Application.Modules.Social.Support;

namespace Zadana.Application.Tests.Application.Social;

public class NotificationPayloadHelperTests
{
    [Fact]
    public void Sanitize_ShouldMinifyAndParseValidJsonPayload()
    {
        var result = NotificationPayloadHelper.Sanitize(
            "  عنوان  ",
            "  Title  ",
            "  جسم عربي  ",
            "  English body  ",
            "  order_status_changed  ",
            """
            {
              "orderId": "11111111-1111-1111-1111-111111111111",
              "action": "placed"
            }
            """);

        result.TitleAr.Should().Be("عنوان");
        result.TitleEn.Should().Be("Title");
        result.BodyAr.Should().Be("جسم عربي");
        result.BodyEn.Should().Be("English body");
        result.Type.Should().Be("order_status_changed");
        result.Data.Should().Be("""{"orderId":"11111111-1111-1111-1111-111111111111","action":"placed"}""");
        result.DataObject.HasValue.Should().BeTrue();
        result.DataObject!.Value.GetProperty("action").GetString().Should().Be("placed");
    }

    [Fact]
    public void Sanitize_WhenPayloadIsTooLarge_ShouldStoreValidTruncatedJsonEnvelope()
    {
        var veryLargePayload = JsonSerializer.Serialize(new
        {
            message = new string('x', 6000)
        });

        var result = NotificationPayloadHelper.Sanitize(
            "title",
            "title",
            "body",
            "body",
            "bulk_notification",
            veryLargePayload);

        result.Data.Should().NotBeNull();
        result.Data!.Length.Should().BeLessThanOrEqualTo(4000);
        result.DataObject.HasValue.Should().BeTrue();
        result.DataObject!.Value.GetProperty("truncated").GetBoolean().Should().BeTrue();
        result.DataObject!.Value.GetProperty("originalLength").GetInt32().Should().BeGreaterThan(4000);
        result.DataObject!.Value.GetProperty("excerpt").GetString().Should().NotBeNullOrEmpty();
    }
}
