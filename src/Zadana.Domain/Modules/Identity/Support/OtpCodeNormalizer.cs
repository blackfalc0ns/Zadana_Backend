namespace Zadana.Domain.Modules.Identity.Support;

public static class OtpCodeNormalizer
{
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var buffer = new char[code.Length];
        var count = 0;
        foreach (var character in code)
        {
            var digit = character switch
            {
                >= '0' and <= '9' => character,
                >= '٠' and <= '٩' => (char)('0' + (character - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (character - '۰')),
                _ => '\0'
            };

            if (digit != '\0')
            {
                buffer[count++] = digit;
            }
        }

        return new string(buffer, 0, count);
    }
}
