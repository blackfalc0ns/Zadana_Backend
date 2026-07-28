namespace Zadana.Application.Modules.Vendors.DTOs;

public record AdminVendorStatsDto(
    int TotalVendors,
    int PendingApproval,
    int MissingDocuments,
    int HighRisk,
    int PayoutBlocked,
    int Suspended);
