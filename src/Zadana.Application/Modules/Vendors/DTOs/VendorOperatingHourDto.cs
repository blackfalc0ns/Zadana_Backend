namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorOperatingHourDto(
    int DayOfWeek,
    string OpenTime,
    string CloseTime,
    bool IsOpen);
