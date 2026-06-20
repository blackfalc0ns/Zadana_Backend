using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorProfileReviewCatalog
{
    public const string Step1BusinessNameAr = "step1.businessNameAr";
    public const string Step1BusinessNameEn = "step1.businessNameEn";
    public const string Step1BusinessType = "step1.businessType";
    public const string Step1ContactPhone = "step1.contactPhone";
    public const string Step1Description = "step1.description";
    public const string Step1OwnerName = "step1.ownerName";
    public const string Step1OwnerEmail = "step1.ownerEmail";
    public const string Step1OwnerPhone = "step1.ownerPhone";
    public const string Step2Region = "step2.region";
    public const string Step2City = "step2.city";
    public const string Step2NationalAddress = "step2.nationalAddress";
    public const string Step2BranchLatitude = "step2.branchLatitude";
    public const string Step2BranchLongitude = "step2.branchLongitude";
    public const string Step3IdNumber = "step3.idNumber";
    public const string Step3Nationality = "step3.nationality";
    public const string Step3CommercialRegistrationNumber = "step3.commercialRegistrationNumber";
    public const string Step3ExpiryDate = "step3.expiryDate";
    public const string Step3TaxId = "step3.taxId";
    public const string Step3LicenseNumber = "step3.licenseNumber";
    public const string Step4BankName = "step4.bankName";
    public const string Step4PaymentCycle = "step4.paymentCycle";
    public const string Step4Iban = "step4.iban";
    public const string Step4SwiftCode = "step4.swiftCode";
    public const string Step5Logo = "step5.logo";
    public const string Step5Commercial = "step5.commercial";
    public const string Step5Tax = "step5.tax";
    public const string Step5License = "step5.license";

    public static readonly IReadOnlyList<ReviewDefinition> Definitions =
    [
        new(Step1BusinessNameAr, 1, VendorProfileReviewTargetType.Field, true, vendor => vendor.BusinessNameAr),
        new(Step1BusinessNameEn, 1, VendorProfileReviewTargetType.Field, true, vendor => vendor.BusinessNameEn),
        new(Step1BusinessType, 1, VendorProfileReviewTargetType.Field, true, vendor => vendor.BusinessType),
        new(Step1ContactPhone, 1, VendorProfileReviewTargetType.Field, true, vendor => vendor.ContactPhone),
        new(Step1Description, 1, VendorProfileReviewTargetType.Field, true, vendor => vendor.DescriptionAr ?? vendor.DescriptionEn),
        new(Step1OwnerName, 1, VendorProfileReviewTargetType.Field, true, vendor => vendor.OwnerName),
        new(Step1OwnerEmail, 1, VendorProfileReviewTargetType.Field, true, vendor => vendor.OwnerEmail),
        new(Step1OwnerPhone, 1, VendorProfileReviewTargetType.Field, true, vendor => vendor.OwnerPhone),
        new(Step2Region, 2, VendorProfileReviewTargetType.Field, true, vendor => vendor.Region),
        new(Step2City, 2, VendorProfileReviewTargetType.Field, true, vendor => vendor.City),
        new(Step2NationalAddress, 2, VendorProfileReviewTargetType.Field, true, vendor => vendor.NationalAddress),
        new(Step2BranchLatitude, 2, VendorProfileReviewTargetType.Field, true, vendor => null),
        new(Step2BranchLongitude, 2, VendorProfileReviewTargetType.Field, true, vendor => null),
        new(Step3IdNumber, 3, VendorProfileReviewTargetType.Field, true, vendor => vendor.IdNumber),
        new(Step3Nationality, 3, VendorProfileReviewTargetType.Field, true, vendor => vendor.Nationality),
        new(Step3CommercialRegistrationNumber, 3, VendorProfileReviewTargetType.Field, true, vendor => vendor.CommercialRegistrationNumber),
        new(Step3ExpiryDate, 3, VendorProfileReviewTargetType.Field, true, vendor => vendor.CommercialRegistrationExpiryDate?.ToString("O")),
        new(Step3TaxId, 3, VendorProfileReviewTargetType.Field, true, vendor => vendor.TaxId),
        new(Step3LicenseNumber, 3, VendorProfileReviewTargetType.Field, false, vendor => vendor.LicenseNumber),
        new(Step4BankName, 4, VendorProfileReviewTargetType.Field, true, vendor => null),
        new(Step4PaymentCycle, 4, VendorProfileReviewTargetType.Field, true, vendor => vendor.PayoutCycle),
        new(Step4Iban, 4, VendorProfileReviewTargetType.Field, true, vendor => null),
        new(Step4SwiftCode, 4, VendorProfileReviewTargetType.Field, true, vendor => null),
        new(Step5Logo, 5, VendorProfileReviewTargetType.Document, true, vendor => vendor.LogoUrl),
        new(Step5Commercial, 5, VendorProfileReviewTargetType.Document, true, vendor => vendor.CommercialRegisterDocumentUrl),
        new(Step5Tax, 5, VendorProfileReviewTargetType.Document, true, vendor => vendor.TaxDocumentUrl),
        new(Step5License, 5, VendorProfileReviewTargetType.Document, true, vendor => vendor.LicenseDocumentUrl)
    ];

    public static readonly IReadOnlyDictionary<string, ReviewDefinition> DefinitionsByCode =
        Definitions.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SectionCodes =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["store"] =
            [
                Step1BusinessNameAr, Step1BusinessNameEn, Step1BusinessType, Step1ContactPhone,
                Step1Description, Step5Logo
            ],
            ["owner"] =
            [
                Step1OwnerName, Step1OwnerEmail, Step1OwnerPhone, Step3IdNumber, Step3Nationality
            ],
            ["contact"] =
            [
                Step2Region, Step2City, Step2NationalAddress, Step2BranchLatitude, Step2BranchLongitude
            ],
            ["legal"] =
            [
                Step3CommercialRegistrationNumber, Step3ExpiryDate, Step3TaxId, Step3LicenseNumber
            ],
            ["banking"] =
            [
                Step4BankName, Step4PaymentCycle, Step4Iban, Step4SwiftCode
            ]
        };

    public static bool TryGetDefinition(string code, out ReviewDefinition definition) =>
        DefinitionsByCode.TryGetValue(code, out definition!);

    public static IReadOnlyList<string> GetSectionCodes(string section) =>
        SectionCodes.TryGetValue(section, out var codes) ? codes : [];

    public static bool TryResolveSection(string code, out string section)
    {
        foreach (var (sectionKey, codes) in SectionCodes)
        {
            if (codes.Any(item => string.Equals(item, code, StringComparison.OrdinalIgnoreCase)))
            {
                section = sectionKey;
                return true;
            }
        }

        if (string.Equals(code, Step5Commercial, StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, Step5Tax, StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, Step5License, StringComparison.OrdinalIgnoreCase))
        {
            section = "legal";
            return true;
        }

        section = string.Empty;
        return false;
    }

    public static (string LabelAr, string LabelEn) GetSectionLabel(string section) =>
        SectionLabels.TryGetValue(section, out var label) ? label : ("قسم الملف", "Profile section");

    private static readonly IReadOnlyDictionary<string, (string LabelAr, string LabelEn)> SectionLabels =
        new Dictionary<string, (string LabelAr, string LabelEn)>(StringComparer.OrdinalIgnoreCase)
        {
            ["store"] = ("بيانات المتجر", "Store profile"),
            ["owner"] = ("بيانات المالك", "Owner details"),
            ["contact"] = ("بيانات التواصل", "Contact details"),
            ["legal"] = ("البيانات القانونية", "Legal & compliance"),
            ["banking"] = ("البيانات البنكية", "Banking details")
        };

    public static string BuildProfileSectionTab(string section) =>
        string.IsNullOrWhiteSpace(section) ? "store-section" : $"{section.Trim().ToLowerInvariant()}-section";

    public sealed record ReviewDefinition(
        string Code,
        int Step,
        VendorProfileReviewTargetType TargetType,
        bool IsRequired,
        Func<Vendor, string?> ValueAccessor);
}
