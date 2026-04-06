namespace Zadana.Api.Modules.Identity.Requests;

public record RegisterCustomerRequest(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string? ProfilePhotoUrl,
    string AddressLine,
    string? Label,
    string? BuildingNo,
    string? FloorNo,
    string? ApartmentNo,
    string? City,
    string? Area,
    decimal? Latitude,
    decimal? Longitude);
