using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Zadana.Api.BackgroundJobs;
using Zadana.Api.Middleware;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.UnitTests.Common;

public class SystemLogMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RedactsProfileAndBankingSensitiveFields()
    {
        var queue = new RecordingSystemLogQueue();
        var middleware = new SystemLogMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return Task.CompletedTask;
            },
            queue,
            NullLogger<SystemLogMiddleware>.Instance);
        var context = new DefaultHttpContext();
        const string body = """
            {
              "iban": "SA1234567890123456789012",
              "nationalId": "1234567890",
              "commercialRegisterDocumentUrl": "https://cdn.example.com/cr.pdf",
              "nested": {
                "accountIdentifier": "SA9999999999999999999999",
                "licenseImageUrl": "https://cdn.example.com/license.png"
              },
              "safeField": "visible"
            }
            """;

        context.Request.Method = HttpMethods.Put;
        context.Request.Path = "/api/drivers/me/profile/vehicle";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentLength = context.Request.Body.Length;

        await middleware.InvokeAsync(context);

        queue.Entry.Should().NotBeNull();
        queue.Entry!.RequestPayloadJson.Should().Contain("\"iban\":\"***\"");
        queue.Entry.RequestPayloadJson.Should().Contain("\"nationalId\":\"***\"");
        queue.Entry.RequestPayloadJson.Should().Contain("\"commercialRegisterDocumentUrl\":\"***\"");
        queue.Entry.RequestPayloadJson.Should().Contain("\"accountIdentifier\":\"***\"");
        queue.Entry.RequestPayloadJson.Should().Contain("\"licenseImageUrl\":\"***\"");
        queue.Entry.RequestPayloadJson.Should().Contain("\"safeField\":\"visible\"");
        queue.Entry.RequestPayloadJson.Should().NotContain("SA1234567890123456789012");
        queue.Entry.RequestPayloadJson.Should().NotContain("1234567890");
        queue.Entry.RequestPayloadJson.Should().NotContain("https://cdn.example.com/cr.pdf");
    }

    private sealed class RecordingSystemLogQueue : ISystemLogQueue
    {
        public SystemLogEntry? Entry { get; private set; }

        public System.Threading.Channels.ChannelReader<SystemLogEntry> Reader =>
            throw new NotSupportedException();

        public bool TryEnqueue(SystemLogEntry entry)
        {
            Entry = entry;
            return true;
        }
    }
}
