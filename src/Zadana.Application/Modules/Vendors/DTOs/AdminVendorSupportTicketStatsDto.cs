namespace Zadana.Application.Modules.Vendors.DTOs;

public record AdminVendorSupportTicketStatsDto(
    int TotalOpen,
    int WaitingVendor,
    int Resolved);
