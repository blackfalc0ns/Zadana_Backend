using System.Text.Json;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DriverNotificationDataBuilder
{
    public static string Build(
        string screen,
        string @event,
        Guid? orderId = null,
        Guid? assignmentId = null,
        Guid? supportCaseId = null,
        Guid? withdrawalId = null,
        Guid? driverId = null,
        object? extra = null)
    {
        var data = new Dictionary<string, object?>
        {
            ["screen"] = screen,
            ["event"] = @event,
            ["orderId"] = orderId,
            ["assignmentId"] = assignmentId,
            ["supportCaseId"] = supportCaseId,
            ["withdrawalId"] = withdrawalId
        };

        if (driverId.HasValue)
        {
            data["driverId"] = driverId.Value;
        }

        if (extra is not null)
        {
            foreach (var property in JsonSerializer.SerializeToElement(extra).EnumerateObject())
            {
                data[property.Name] = Deserialize(property.Value);
            }
        }

        return JsonSerializer.Serialize(data);
    }

    private static object? Deserialize(JsonElement element) =>
        JsonSerializer.Deserialize<object>(element.GetRawText());
}
