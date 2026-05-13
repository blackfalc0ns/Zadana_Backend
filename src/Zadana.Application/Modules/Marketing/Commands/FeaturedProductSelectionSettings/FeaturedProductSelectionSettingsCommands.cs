using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;
using FeaturedProductSelectionSettingsEntity = Zadana.Domain.Modules.Marketing.Entities.FeaturedProductSelectionSettings;

namespace Zadana.Application.Modules.Marketing.Commands.FeaturedProductSelectionSettings;

public record UpdateFeaturedProductSelectionSettingsCommand(
    string SelectionMode,
    int TargetCount,
    int MinSalesCount,
    int MinStoreCount,
    bool RequireDiscount,
    bool ExcludeProductsAlreadyInSpecialOffers) : IRequest<FeaturedProductSelectionSettingsDto>;

public class UpdateFeaturedProductSelectionSettingsCommandValidator : AbstractValidator<UpdateFeaturedProductSelectionSettingsCommand>
{
    public UpdateFeaturedProductSelectionSettingsCommandValidator()
    {
        RuleFor(x => x.SelectionMode)
            .NotEmpty()
            .Must(FeaturedProductSelectionSettingsHelpers.IsValidSelectionMode);
        RuleFor(x => x.TargetCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinSalesCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinStoreCount).GreaterThanOrEqualTo(0);
    }
}

public class UpdateFeaturedProductSelectionSettingsCommandHandler : IRequestHandler<UpdateFeaturedProductSelectionSettingsCommand, FeaturedProductSelectionSettingsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateFeaturedProductSelectionSettingsCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<FeaturedProductSelectionSettingsDto> Handle(UpdateFeaturedProductSelectionSettingsCommand request, CancellationToken cancellationToken)
    {
        var selectionMode = FeaturedProductSelectionSettingsHelpers.ParseSelectionMode(request.SelectionMode);
        FeaturedProductSelectionSettingsEntity? entity;
        try
        {
            entity = await _context.FeaturedProductSelectionSettings
                .OrderBy(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (MarketingDatabaseObjectFallbacks.IsMissingDatabaseObject(ex))
        {
            return new FeaturedProductSelectionSettingsDto(
                selectionMode.ToString(),
                request.TargetCount,
                request.MinSalesCount,
                request.MinStoreCount,
                request.RequireDiscount,
                request.ExcludeProductsAlreadyInSpecialOffers);
        }

        if (entity is null)
        {
            entity = new FeaturedProductSelectionSettingsEntity(
                selectionMode,
                request.TargetCount,
                request.MinSalesCount,
                request.MinStoreCount,
                request.RequireDiscount,
                request.ExcludeProductsAlreadyInSpecialOffers);
            _context.FeaturedProductSelectionSettings.Add(entity);
        }
        else
        {
            entity.Update(
                selectionMode,
                request.TargetCount,
                request.MinSalesCount,
                request.MinStoreCount,
                request.RequireDiscount,
                request.ExcludeProductsAlreadyInSpecialOffers);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.HomeReadModels, cancellationToken);
        }
        catch (Exception ex) when (MarketingDatabaseObjectFallbacks.IsMissingDatabaseObject(ex))
        {
            return new FeaturedProductSelectionSettingsDto(
                selectionMode.ToString(),
                request.TargetCount,
                request.MinSalesCount,
                request.MinStoreCount,
                request.RequireDiscount,
                request.ExcludeProductsAlreadyInSpecialOffers);
        }

        return new FeaturedProductSelectionSettingsDto(
            entity.SelectionMode.ToString(),
            entity.TargetCount,
            entity.MinSalesCount,
            entity.MinStoreCount,
            entity.RequireDiscount,
            entity.ExcludeProductsAlreadyInSpecialOffers);
    }
}

internal static class FeaturedProductSelectionSettingsHelpers
{
    public static bool IsValidSelectionMode(string value) =>
        Enum.TryParse<FeaturedProductSelectionMode>(value, true, out _);

    public static FeaturedProductSelectionMode ParseSelectionMode(string value)
    {
        if (!Enum.TryParse<FeaturedProductSelectionMode>(value, true, out var parsed))
        {
            throw new BusinessRuleException("INVALID_FEATURED_SELECTION_MODE", "Invalid featured product selection mode.");
        }

        return parsed;
    }
}
