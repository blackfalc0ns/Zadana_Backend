namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorOperationsSettingsDto(
    bool AcceptOrders,
    decimal? MinimumOrderAmount,
    int? PreparationTimeMinutes);
