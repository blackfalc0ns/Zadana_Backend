using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class VendorWorkspaceState : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string Feature { get; private set; } = null!;
    public string PayloadJson { get; private set; } = "{}";
    public DateTime UpdatedAtUtc { get; private set; }

    public Vendor Vendor { get; private set; } = null!;

    private VendorWorkspaceState() { }

    public VendorWorkspaceState(Guid vendorId, string feature, string payloadJson)
    {
        VendorId = vendorId;
        Feature = NormalizeFeature(feature);
        UpdatePayload(payloadJson);
    }

    public void UpdatePayload(string payloadJson)
    {
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static string NormalizeFeature(string feature) => feature.Trim().ToLowerInvariant();
}
