namespace Zadana.Api.Modules.Identity.Requests;

public record AddCustomerAddressRequest(
    string ContactName,
    string ContactPhone,
    string AddressLine,
    string? Label,
    string? BuildingNo,
    string? FloorNo,
    string? ApartmentNo,
    string? City,
    string? Area,
    decimal? Latitude,
    decimal? Longitude);
