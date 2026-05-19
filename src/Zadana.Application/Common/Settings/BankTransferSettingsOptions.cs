namespace Zadana.Application.Common.Settings;

public class BankTransferSettingsOptions
{
    public const string SectionName = "BankTransfer";

    public bool Enabled { get; set; } = true;

    public string ProviderName { get; set; } = "BankTransfer";

    public string BankName { get; set; } = string.Empty;

    public string AccountHolderName { get; set; } = string.Empty;

    public string Iban { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 1440;
}
