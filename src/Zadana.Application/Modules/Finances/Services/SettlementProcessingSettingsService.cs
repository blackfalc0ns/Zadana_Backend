using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Finances.Services;

public interface ISettlementProcessingSettingsService
{
    Task<SettlementProcessingSettings> GetAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAutomaticAsync(CancellationToken cancellationToken = default);
    Task<SettlementProcessingSettings> SetModeAsync(
        SettlementProcessingMode mode,
        Guid changedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Owns the singleton settlement processing setting. Missing rows resolve to
/// Automatic so deployments remain backwards compatible until the setting is
/// first persisted.
/// </summary>
public sealed class SettlementProcessingSettingsService : ISettlementProcessingSettingsService
{
    private readonly IApplicationDbContext _context;

    public SettlementProcessingSettingsService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SettlementProcessingSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.SettlementProcessingSettings
            .FirstOrDefaultAsync(item => item.Id == SettlementProcessingSettings.SingletonId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new SettlementProcessingSettings(SettlementProcessingMode.Automatic);
        _context.SettlementProcessingSettings.Add(settings);
        await _context.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<bool> IsAutomaticAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync(cancellationToken)).Mode == SettlementProcessingMode.Automatic;

    public async Task<SettlementProcessingSettings> SetModeAsync(
        SettlementProcessingMode mode,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new BusinessRuleException(
                "SETTLEMENT_PROCESSING_MODE_INVALID",
                "Settlement processing mode must be Automatic or Manual.");
        }

        if (changedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "USER_NOT_AUTHENTICATED",
                "An authenticated administrator is required to change settlement processing mode.");
        }

        var settings = await GetAsync(cancellationToken);
        if (settings.Mode == mode)
        {
            return settings;
        }

        var previousMode = settings.Mode;
        settings.SetMode(mode, changedByUserId);
        _context.SettlementProcessingModeAudits.Add(new SettlementProcessingModeAudit(
            previousMode,
            mode,
            changedByUserId));

        await _context.SaveChangesAsync(cancellationToken);
        return settings;
    }
}
