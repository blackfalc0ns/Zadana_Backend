using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Marketing.Commands.HomeBanners;

public record CreateHomeBannerCommand(
    string TagAr,
    string TagEn,
    string TitleAr,
    string TitleEn,
    string? SubtitleAr,
    string? SubtitleEn,
    string? ActionLabelAr,
    string? ActionLabelEn,
    string ImageUrl,
    int DisplayOrder,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc) : IRequest<HomeBannerAdminDto>;

public record UpdateHomeBannerCommand(
    Guid Id,
    string TagAr,
    string TagEn,
    string TitleAr,
    string TitleEn,
    string? SubtitleAr,
    string? SubtitleEn,
    string? ActionLabelAr,
    string? ActionLabelEn,
    string ImageUrl,
    int DisplayOrder,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsActive) : IRequest<HomeBannerAdminDto>;

public record ActivateHomeBannerCommand(Guid Id) : IRequest;
public record DeactivateHomeBannerCommand(Guid Id) : IRequest;
public record DeleteHomeBannerCommand(Guid Id) : IRequest;

public class CreateHomeBannerCommandValidator : AbstractValidator<CreateHomeBannerCommand>
{
    public CreateHomeBannerCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.TagAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TagEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndsAtUtc)
            .GreaterThanOrEqualTo(x => x.StartsAtUtc!.Value)
            .When(x => x.StartsAtUtc.HasValue && x.EndsAtUtc.HasValue)
            .WithMessage(localizer["InvalidDateRange"]);
    }
}

public class UpdateHomeBannerCommandValidator : AbstractValidator<UpdateHomeBannerCommand>
{
    public UpdateHomeBannerCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.TagAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TagEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndsAtUtc)
            .GreaterThanOrEqualTo(x => x.StartsAtUtc!.Value)
            .When(x => x.StartsAtUtc.HasValue && x.EndsAtUtc.HasValue)
            .WithMessage(localizer["InvalidDateRange"]);
    }
}

public class CreateHomeBannerCommandHandler : IRequestHandler<CreateHomeBannerCommand, HomeBannerAdminDto>
{
    private readonly IApplicationDbContext _context;
    public CreateHomeBannerCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<HomeBannerAdminDto> Handle(CreateHomeBannerCommand request, CancellationToken cancellationToken)
    {
        var entity = new HomeBanner(
            request.TagAr, request.TagEn, request.TitleAr, request.TitleEn, request.ImageUrl,
            request.SubtitleAr, request.SubtitleEn, request.ActionLabelAr, request.ActionLabelEn,
            request.DisplayOrder, request.StartsAtUtc, request.EndsAtUtc);

        _context.HomeBanners.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return MarketingMappings.ToDto(entity);
    }
}

public class UpdateHomeBannerCommandHandler : IRequestHandler<UpdateHomeBannerCommand, HomeBannerAdminDto>
{
    private readonly IApplicationDbContext _context;
    public UpdateHomeBannerCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<HomeBannerAdminDto> Handle(UpdateHomeBannerCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeBanners.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(HomeBanner), request.Id);

        entity.UpdateContent(
            request.TagAr, request.TagEn, request.TitleAr, request.TitleEn, request.ImageUrl,
            request.SubtitleAr, request.SubtitleEn, request.ActionLabelAr, request.ActionLabelEn,
            request.DisplayOrder, request.StartsAtUtc, request.EndsAtUtc);

        if (request.IsActive) entity.Activate(); else entity.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);
        return MarketingMappings.ToDto(entity);
    }
}

public class ActivateHomeBannerCommandHandler : IRequestHandler<ActivateHomeBannerCommand>
{
    private readonly IApplicationDbContext _context;
    public ActivateHomeBannerCommandHandler(IApplicationDbContext context) => _context = context;
    public async Task Handle(ActivateHomeBannerCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeBanners.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(HomeBanner), request.Id);
        entity.Activate();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class DeactivateHomeBannerCommandHandler : IRequestHandler<DeactivateHomeBannerCommand>
{
    private readonly IApplicationDbContext _context;
    public DeactivateHomeBannerCommandHandler(IApplicationDbContext context) => _context = context;
    public async Task Handle(DeactivateHomeBannerCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeBanners.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(HomeBanner), request.Id);
        entity.Deactivate();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class DeleteHomeBannerCommandHandler : IRequestHandler<DeleteHomeBannerCommand>
{
    private readonly IApplicationDbContext _context;
    public DeleteHomeBannerCommandHandler(IApplicationDbContext context) => _context = context;
    public async Task Handle(DeleteHomeBannerCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeBanners.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(HomeBanner), request.Id);
        _context.HomeBanners.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

internal static partial class MarketingMappings
{
    public static HomeBannerAdminDto ToDto(HomeBanner entity) =>
        new(
            entity.Id,
            entity.TagAr,
            entity.TagEn,
            entity.TitleAr,
            entity.TitleEn,
            entity.SubtitleAr,
            entity.SubtitleEn,
            entity.ActionLabelAr,
            entity.ActionLabelEn,
            entity.ImageUrl,
            entity.DisplayOrder,
            entity.IsActive,
            entity.StartsAtUtc,
            entity.EndsAtUtc,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
}
