using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class EmailSenderProfileConfig : BaseEntity
{
    public string ProfileKey { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public string ReplyTo { get; private set; } = null!;
    public string DescriptionKey { get; private set; } = null!;
    public string Locale { get; private set; } = null!;
    public bool IsDefault { get; private set; }
    public string Status { get; private set; } = null!;
    public bool IsReadOnly { get; private set; }

    private EmailSenderProfileConfig() { }

    public EmailSenderProfileConfig(
        string profileKey,
        string name,
        string address,
        string replyTo,
        string descriptionKey,
        string locale,
        bool isDefault,
        string status,
        bool isReadOnly = true)
    {
        ProfileKey = profileKey.Trim();
        Name = name.Trim();
        Address = address.Trim().ToLowerInvariant();
        ReplyTo = replyTo.Trim().ToLowerInvariant();
        DescriptionKey = descriptionKey.Trim();
        Locale = locale.Trim().ToLowerInvariant();
        IsDefault = isDefault;
        Status = status.Trim().ToLowerInvariant();
        IsReadOnly = isReadOnly;
    }
}
