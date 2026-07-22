using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Identity.Services;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// Idempotently migrates legacy plaintext vendor PII, bank details and
/// approval payloads through the configured EF encryption converter.
/// Raw SQL predicates inspect the stored representation, while materializing
/// through EF decrypts legacy values before they are written as enc:v2.
/// </summary>
public sealed class VendorPiiEncryptionBackfillTask : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VendorPiiEncryptionBackfillTask> _logger;

    public VendorPiiEncryptionBackfillTask(
        IServiceScopeFactory scopeFactory,
        ILogger<VendorPiiEncryptionBackfillTask> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var vendors = await db.Vendors
                .FromSqlRaw("""
                    SELECT *
                    FROM [Vendor]
                    WHERE [CommercialRegistrationNumberHash] IS NULL
                       OR [CommercialRegistrationNumber] NOT LIKE 'enc:v2:%'
                       OR ([TaxId] IS NOT NULL AND [TaxId] NOT LIKE 'enc:v2:%')
                       OR [ContactEmail] NOT LIKE 'enc:v2:%'
                       OR [ContactPhone] NOT LIKE 'enc:v2:%'
                       OR ([OwnerName] IS NOT NULL AND [OwnerName] NOT LIKE 'enc:v2:%')
                       OR ([OwnerEmail] IS NOT NULL AND [OwnerEmail] NOT LIKE 'enc:v2:%')
                       OR ([OwnerPhone] IS NOT NULL AND [OwnerPhone] NOT LIKE 'enc:v2:%')
                       OR ([IdNumber] IS NOT NULL AND [IdNumber] NOT LIKE 'enc:v2:%')
                       OR ([NationalAddress] IS NOT NULL AND [NationalAddress] NOT LIKE 'enc:v2:%')
                       OR ([LicenseNumber] IS NOT NULL AND [LicenseNumber] NOT LIKE 'enc:v2:%')
                    ORDER BY [Id]
                    """)
                .ToListAsync(cancellationToken);

            foreach (var vendor in vendors)
            {
                var entry = db.Entry(vendor);
                var hash = SearchableHashProvider.Compute(
                    vendor.CommercialRegistrationNumber?.Trim().ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(hash))
                {
                    entry.Property(nameof(vendor.CommercialRegistrationNumberHash)).CurrentValue = hash;
                }

                MarkIfPresent(entry, nameof(vendor.CommercialRegistrationNumber), vendor.CommercialRegistrationNumber);
                MarkIfPresent(entry, nameof(vendor.TaxId), vendor.TaxId);
                MarkIfPresent(entry, nameof(vendor.ContactEmail), vendor.ContactEmail);
                MarkIfPresent(entry, nameof(vendor.ContactPhone), vendor.ContactPhone);
                MarkIfPresent(entry, nameof(vendor.OwnerName), vendor.OwnerName);
                MarkIfPresent(entry, nameof(vendor.OwnerEmail), vendor.OwnerEmail);
                MarkIfPresent(entry, nameof(vendor.OwnerPhone), vendor.OwnerPhone);
                MarkIfPresent(entry, nameof(vendor.IdNumber), vendor.IdNumber);
                MarkIfPresent(entry, nameof(vendor.NationalAddress), vendor.NationalAddress);
                MarkIfPresent(entry, nameof(vendor.LicenseNumber), vendor.LicenseNumber);
            }

            var bankAccounts = await db.VendorBankAccounts
                .FromSqlRaw("""
                    SELECT *
                    FROM [VendorBankAccount]
                    WHERE [IBAN] NOT LIKE 'enc:v2:%'
                       OR [AccountHolderName] NOT LIKE 'enc:v2:%'
                    ORDER BY [Id]
                    """)
                .ToListAsync(cancellationToken);

            foreach (var account in bankAccounts)
            {
                var entry = db.Entry(account);
                MarkIfPresent(entry, nameof(account.IBAN), account.IBAN);
                MarkIfPresent(entry, nameof(account.AccountHolderName), account.AccountHolderName);
            }

            var driverPayoutMethods = await db.DriverPayoutMethods
                .FromSqlRaw("""
                    SELECT *
                    FROM [DriverPayoutMethods]
                    WHERE [AccountIdentifier] NOT LIKE 'enc:v2:%'
                       OR [AccountHolderName] NOT LIKE 'enc:v2:%'
                    ORDER BY [Id]
                    """)
                .ToListAsync(cancellationToken);

            foreach (var method in driverPayoutMethods)
            {
                var entry = db.Entry(method);
                MarkIfPresent(entry, nameof(method.AccountIdentifier), method.AccountIdentifier);
                MarkIfPresent(entry, nameof(method.AccountHolderName), method.AccountHolderName);
            }

            var approvals = await db.AccessApprovalRequests
                .FromSqlRaw("""
                    SELECT *
                    FROM [AccessApprovalRequests]
                    WHERE [PayloadJson] NOT LIKE 'enc:v2:%'
                    ORDER BY [CreatedAtUtc], [Id]
                    """)
                .ToListAsync(cancellationToken);

            foreach (var approval in approvals)
            {
                MarkIfPresent(db.Entry(approval), nameof(approval.PayloadJson), approval.PayloadJson);
            }

            var updated = await db.SaveChangesAsync(cancellationToken);
            if (updated > 0)
            {
                _logger.LogInformation(
                    "Financial PII encryption backfill updated {Count} records.",
                    updated);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Financial PII encryption backfill failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void MarkIfPresent<TEntity>(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> entry,
        string propertyName,
        string? value)
        where TEntity : class
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entry.Property(propertyName).IsModified = true;
        }
    }
}
