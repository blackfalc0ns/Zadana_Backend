namespace Zadana.Api.Security;

public static class RateLimitPolicyNames
{
    public const string Auth = "auth";
    public const string FileUploads = "file-uploads";
    public const string PaymentCallbacks = "payment-callbacks";
    public const string WalletMutations = "wallet-mutations";
}
