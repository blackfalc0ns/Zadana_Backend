namespace Zadana.Application.Modules.Identity.DTOs;

public record CustomerAddressDto(
    Guid Id,
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
    decimal? Longitude,
    bool IsDefault);
