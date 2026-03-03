using System.Collections.Generic;

namespace Zadana.SharedKernel.Primitives;

public class LocalizedString : ValueObject
{
    public string Ar { get; private set; }
    public string En { get; private set; }

    // Parameterless constructor for EF Core
    private LocalizedString() 
    {
        Ar = string.Empty;
        En = string.Empty;
    }

    public LocalizedString(string ar, string en)
    {
        Ar = ar ?? string.Empty;
        En = en ?? string.Empty;
    }

    // Optional helper method to get the value based on the current culture
    public string GetValue(string languageCode)
    {
        return languageCode?.ToLower() == "ar" ? Ar : En;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Ar;
        yield return En;
    }
}
