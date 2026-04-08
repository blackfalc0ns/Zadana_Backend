using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Marketing.Commands.HomeSections;

public record CreateHomeSectionCommand(
    Guid CategoryId,
    string Theme,
    int DisplayOrder,
    int ProductsTake,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc) : IRequest<HomeSectionAdminDto>;

public record UpdateHomeSectionCommand(
    Guid Id,
    Guid CategoryId,
    string Theme,
    int DisplayOrder,
    int ProductsTake,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsActive) : IRequest<HomeSectionAdminDto>;

public record ActivateHomeSectionCommand(Guid Id) : IRequest;
public record DeactivateHomeSectionCommand(Guid Id) : IRequest;
public record DeleteHomeSectionCommand(Guid Id) : IRequest;

public class CreateHomeSectionCommandValidator : AbstractValidator<CreateHomeSectionCommand>
{
    public CreateHomeSectionCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Theme).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProductsTake).InclusiveBetween(1, 20);
        RuleFor(x => x.EndsAtUtc)
            .GreaterThanOrEqualTo(x => x.StartsAtUtc!.Value)
            .When(x => x.StartsAtUtc.HasValue && x.EndsAtUtc.HasValue)
            .WithMessage(localizer["InvalidDateRange"]);
    }
}

public class UpdateHomeSectionCommandValidator : AbstractValidator<UpdateHomeSectionCommand>
{
    public UpdateHomeSectionCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Theme).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProductsTake).InclusiveBetween(1, 20);
        RuleFor(x => x.EndsAtUtc)
            .GreaterThanOrEqualTo(x => x.StartsAtUtc!.Value)
            .When(x => x.StartsAtUtc.HasValue && x.EndsAtUtc.HasValue)
            .WithMessage(localizer["InvalidDateRange"]);
    }
}

public class CreateHomeSectionCommandHandler : IRequestHandler<CreateHomeSectionCommand, HomeSectionAdminDto>
{
    private readonly IApplicationDbContext _context;
    public CreateHomeSectionCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<HomeSectionAdminDto> Handle(CreateHomeSectionCommand request, CancellationToken cancellationToken)
    {
        await _context.ValidateHomeSectionCategoryAsync(request.CategoryId, cancellationToken);

        var entity = new HomeSection(
            request.CategoryId,
            request.Theme,
            request.DisplayOrder,
            request.ProductsTake,
            request.StartsAtUtc,
            request.EndsAtUtc);

        _context.HomeSections.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return await _context.ProjectHomeSectionAsync(entity.Id, cancellationToken);
    }
}

public class UpdateHomeSectionCommandHandler : IRequestHandler<UpdateHomeSectionCommand, HomeSectionAdminDto>
{
    private readonly IApplicationDbContext _context;
    public UpdateHomeSectionCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<HomeSectionAdminDto> Handle(UpdateHomeSectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeSections.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(HomeSection), request.Id);

        await _context.ValidateHomeSectionCategoryAsync(request.CategoryId, cancellationToken);

        entity.Update(
            request.CategoryId,
            request.Theme,
            request.DisplayOrder,
            request.ProductsTake,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.IsActive);

        await _context.SaveChangesAsync(cancellationToken);
        return await _context.ProjectHomeSectionAsync(entity.Id, cancellationToken);
    }
}

public class ActivateHomeSectionCommandHandler : IRequestHandler<ActivateHomeSectionCommand>
{
    private readonly IApplicationDbContext _context;
    public ActivateHomeSectionCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(ActivateHomeSectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeSections.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(HomeSection), request.Id);
        entity.Activate();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class DeactivateHomeSectionCommandHandler : IRequestHandler<DeactivateHomeSectionCommand>
{
    private readonly IApplicationDbContext _context;
    public DeactivateHomeSectionCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(DeactivateHomeSectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeSections.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(HomeSection), request.Id);
        entity.Deactivate();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class DeleteHomeSectionCommandHandler : IRequestHandler<DeleteHomeSectionCommand>
{
    private readonly IApplicationDbContext _context;
    public DeleteHomeSectionCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(DeleteHomeSectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeSections.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(HomeSection), request.Id);
        _context.HomeSections.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

internal static partial class MarketingMappings
{
    public static HomeSectionAdminDto ToDto(HomeSection entity, string categoryNameAr, string categoryNameEn) =>
        new(
            entity.Id,
            entity.CategoryId,
            categoryNameAr,
            categoryNameEn,
            entity.Theme,
            entity.DisplayOrder,
            entity.ProductsTake,
            entity.IsActive,
            entity.StartsAtUtc,
            entity.EndsAtUtc,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
}

internal static class HomeSectionHelpers
{
    public static async Task ValidateHomeSectionCategoryAsync(
        this IApplicationDbContext context,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var category = await context.Categories
            .AsNoTracking()
            .Where(x => x.Id == categoryId && x.IsActive)
            .Select(x => new { x.Id, x.ParentCategoryId })
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            throw new BusinessRuleException("INVALID_HOME_SECTION_CATEGORY", "Selected category does not exist or is inactive.");
        }

        if (!category.ParentCategoryId.HasValue)
        {
            throw new BusinessRuleException("INVALID_HOME_SECTION_SUBCATEGORY", "Home section must reference a sub-category.");
        }
    }

    public static async Task<HomeSectionAdminDto> ProjectHomeSectionAsync(
        this IApplicationDbContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        var projection = await context.HomeSections
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                Entity = x,
                x.Category.NameAr,
                x.Category.NameEn
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(HomeSection), id);

        return MarketingMappings.ToDto(projection.Entity, projection.NameAr, projection.NameEn);
    }
}
