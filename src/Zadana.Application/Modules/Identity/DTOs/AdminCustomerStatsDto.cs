namespace Zadana.Application.Modules.Identity.DTOs;

public record AdminCustomerStatsDto(
    int TotalCustomers,
    int ActiveCustomers,
    int NewCustomers,
    int HighRiskCustomers,
    int ComplaintCustomers,
    int RepeatRefundCustomers);
