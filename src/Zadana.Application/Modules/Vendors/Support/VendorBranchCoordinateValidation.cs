using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorBranchCoordinateValidation
{
    public static bool AreMeaningful(decimal latitude, decimal longitude) =>
        VendorBranchCoordinates.IsValid(latitude, longitude);

    public static bool AreMeaningful(decimal? latitude, decimal? longitude) =>
        latitude.HasValue &&
        longitude.HasValue &&
        VendorBranchCoordinates.IsValid(latitude.Value, longitude.Value);

    public static bool AreBothMissingOrBothMeaningful(decimal? latitude, decimal? longitude)
    {
        if (!latitude.HasValue && !longitude.HasValue)
        {
            return true;
        }

        return AreMeaningful(latitude, longitude);
    }

    public static void EnsureRequired(decimal? latitude, decimal? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            throw new BusinessRuleException(
                "BRANCH_COORDINATES_REQUIRED",
                "لازم تحدد موقع الفرع على الخريطة.|Branch map coordinates are required.");
        }

        EnsureValid(latitude.Value, longitude.Value);
    }

    public static void EnsureValid(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new BusinessRuleException(
                "BRANCH_LATITUDE_INVALID",
                "Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new BusinessRuleException(
                "BRANCH_LONGITUDE_INVALID",
                "Longitude must be between -180 and 180.");
        }

        if (VendorBranchCoordinates.IsUnset(latitude, longitude))
        {
            throw new BusinessRuleException(
                "BRANCH_COORDINATES_REQUIRED",
                "لازم تحدد موقع الفرع على الخريطة.|Branch map coordinates are required.");
        }
    }
}
