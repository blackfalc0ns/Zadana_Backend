using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Marketing.Commands.FeaturedPlacements;

public record CreateFeaturedProductPlacementCommand(
    string PlacementType,
    Guid? VendorProductId,
    Guid? MasterProductId,
    int DisplayOrder,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    string? Note) : IRequest<FeaturedProductPlacementDto>;

public record UpdateFeaturedProductPlacementCommand(
    Guid Id,
    string PlacementType,
    Guid? VendorProductId,
    Guid? MasterProductId,
    int DisplayOrder,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsActive,
    string? Note) : IRequest<FeaturedProductPlacementDto>;

public record ActivateFeaturedProductPlacementCommand(Guid Id) : IRequest;
public record DeactivateFeaturedProductPlacementCommand(Guid Id) : IRequest;
public record DeleteFeaturedProductPlacementCommand(Guid Id) : IRequest;

public class CreateFeaturedProductPlacementCommandValidator : AbstractValidator<CreateFeaturedProductPlacementCommand>
{
    public CreateFeaturedProductPlacementCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.PlacementType).NotEmpty().IsEnumName(typeof(FeaturedPlacementType), false);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndsAtUtc)
            .GreaterThanOrEqualTo(x => x.StartsAtUtc!.Value)
            .When(x => x.StartsAtUtc.HasValue && x.EndsAtUtc.HasValue)
            .WithMessage(localizer["InvalidDateRange"]);
    }
}

public class UpdateFeaturedProductPlacementCommandValidator : AbstractValidator<UpdateFeaturedProductPlacementCommand>
{
    public UpdateFeaturedProductPlacementCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.PlacementType).NotEmpty().IsEnumName(typeof(FeaturedPlacementType), false);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndsAtUtc)
            .GreaterThanOrEqualTo(x => x.StartsAtUtc!.Value)
            .When(x => x.StartsAtUtc.HasValue && x.EndsAtUtc.HasValue)
            .WithMessage(localizer["InvalidDateRange"]);
    }
}

public class CreateFeaturedProductPlacementCommandHandler : IRequestHandler<CreateFeaturedProductPlacementCommand, FeaturedProductPlacementDto>
{
    private readonly IApplicationDbContext _context;
    public CreateFeaturedProductPlacementCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<FeaturedProductPlacementDto> Handle(CreateFeaturedProductPlacementCommand request, CancellationToken cancellationToken)
    {
        var placementType = MarketingPlacementHelpers.ParseType(request.PlacementType);
        await _context.ValidateTargetAsync(placementType, request.VendorProductId, request.MasterProductId, cancellationToken);

        var entity = new FeaturedProductPlacement(
            placementType,
            request.DisplayOrder,
            request.VendorProductId,
            request.MasterProductId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.Note);

        _context.FeaturedProductPlacements.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return await _context.ProjectPlacementAsync(entity.Id, cancellationToken);
    }
}

public class UpdateFeaturedProductPlacementCommandHandler : IRequestHandler<UpdateFeaturedProductPlacementCommand, FeaturedProductPlacementDto>
{
    private readonly IApplicationDbContext _context;
    public UpdateFeaturedProductPlacementCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<FeaturedProductPlacementDto> Handle(UpdateFeaturedProductPlacementCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.FeaturedProductPlacements.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FeaturedProductPlacement), request.Id);

        var placementType = MarketingPlacementHelpers.ParseType(request.PlacementType);
        await _context.ValidateTargetAsync(placementType, request.VendorProductId, request.MasterProductId, cancellationToken);

        entity.Update(
            placementType,
            request.DisplayOrder,
            request.VendorProductId,
            request.MasterProductId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.Note,
            request.IsActive);

        await _context.SaveChangesAsync(cancellationToken);
        return await _context.ProjectPlacementAsync(entity.Id, cancellationToken);
    }
}

public class ActivateFeaturedProductPlacementCommandHandler : IRequestHandler<ActivateFeaturedProductPlacementCommand>
{
    private readonly IApplicationDbContext _context;
    public ActivateFeaturedProductPlacementCommandHandler(IApplicationDbContext context) => _context = context;
    public async Task Handle(ActivateFeaturedProductPlacementCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.FeaturedProductPlacements.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FeaturedProductPlacement), request.Id);
        entity.Activate();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class DeactivateFeaturedProductPlacementCommandHandler : IRequestHandler<DeactivateFeaturedProductPlacementCommand>
{
    private readonly IApplicationDbContext _context;
    public DeactivateFeaturedProductPlacementCommandHandler(IApplicationDbContext context) => _context = context;
    public async Task Handle(DeactivateFeaturedProductPlacementCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.FeaturedProductPlacements.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FeaturedProductPlacement), request.Id);
        entity.Deactivate();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class DeleteFeaturedProductPlacementCommandHandler : IRequestHandler<DeleteFeaturedProductPlacementCommand>
{
    private readonly IApplicationDbContext _context;
    public DeleteFeaturedProductPlacementCommandHandler(IApplicationDbContext context) => _context = context;
    public async Task Handle(DeleteFeaturedProductPlacementCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.FeaturedProductPlacements.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FeaturedProductPlacement), request.Id);
        _context.FeaturedProductPlacements.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

internal static partial class MarketingMappings
{
    public static FeaturedProductPlacementDto ToDto(
        FeaturedProductPlacement entity,
        string? displayNameAr,
        string? displayNameEn) =>
        new(
            entity.Id,
            entity.PlacementType.ToString(),
            entity.VendorProductId,
            entity.MasterProductId,
            displayNameAr,
            displayNameEn,
            entity.DisplayOrder,
            entity.IsActive,
            entity.StartsAtUtc,
            entity.EndsAtUtc,
            entity.Note,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
}

internal static class MarketingPlacementHelpers
{
    public static FeaturedPlacementType ParseType(string value)
    {
        if (!Enum.TryParse<FeaturedPlacementType>(value, true, out var parsed))
        {
            throw new BusinessRuleException("INVALID_FEATURED_PLACEMENT_TYPE", "Invalid featured placement type.");
        }

        return parsed;
    }

    public static async Task ValidateTargetAsync(
        this IApplicationDbContext context,
        FeaturedPlacementType placementType,
        Guid? vendorProductId,
        Guid? masterProductId,
        CancellationToken cancellationToken)
    {
        switch (placementType)
        {
            case FeaturedPlacementType.VendorProduct:
                if (!vendorProductId.HasValue || masterProductId.HasValue)
                    throw new BusinessRuleException("INVALID_FEATURED_PLACEMENT", "Vendor product placement must reference only VendorProductId.");

                var isValidVendorProduct = await context.VendorProducts.AnyAsync(x =>
                    x.Id == vendorProductId.Value &&
                    x.Status == VendorProductStatus.Active &&
                    x.IsAvailable &&
                    x.StockQuantity > 0 &&
                    x.MasterProduct.Status == ProductStatus.Active &&
                    x.Vendor.Status == VendorStatus.Active &&
                    x.Vendor.AcceptOrders,
                    cancellationToken);

                if (!isValidVendorProduct)
                    throw new BusinessRuleException("INVALID_FEATURED_VENDOR_PRODUCT", "Selected vendor product is not eligible for featured placement.");
                break;

            case FeaturedPlacementType.MasterProduct:
                if (!masterProductId.HasValue || vendorProductId.HasValue)
                    throw new BusinessRuleException("INVALID_FEATURED_PLACEMENT", "Master product placement must reference only MasterProductId.");

                var isValidMasterProduct = await context.MasterProducts.AnyAsync(x =>
                    x.Id == masterProductId.Value &&
                    x.Status == ProductStatus.Active,
                    cancellationToken);

                if (!isValidMasterProduct)
                    throw new BusinessRuleException("INVALID_FEATURED_MASTER_PRODUCT", "Selected master product is not eligible for featured placement.");
                break;
        }
    }

    public static async Task<FeaturedProductPlacementDto> ProjectPlacementAsync(
        this IApplicationDbContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        var projection = await context.FeaturedProductPlacements
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                Entity = x,
                DisplayNameAr = x.PlacementType == FeaturedPlacementType.VendorProduct
                    ? (!string.IsNullOrWhiteSpace(x.VendorProduct!.CustomNameAr) ? x.VendorProduct.CustomNameAr : x.VendorProduct.MasterProduct.NameAr)
                    : x.MasterProduct!.NameAr,
                DisplayNameEn = x.PlacementType == FeaturedPlacementType.VendorProduct
                    ? (!string.IsNullOrWhiteSpace(x.VendorProduct!.CustomNameEn) ? x.VendorProduct.CustomNameEn : x.VendorProduct.MasterProduct.NameEn)
                    : x.MasterProduct!.NameEn
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(FeaturedProductPlacement), id);

        return MarketingMappings.ToDto(projection.Entity, projection.DisplayNameAr, projection.DisplayNameEn);
    }
}
