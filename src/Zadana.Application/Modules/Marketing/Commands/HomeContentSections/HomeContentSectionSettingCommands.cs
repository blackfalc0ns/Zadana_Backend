using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Marketing.Commands.HomeContentSections;

public record UpdateHomeContentSectionSettingCommand(string SectionType, bool IsEnabled) : IRequest<HomeContentSectionSettingDto>;
public record ActivateHomeContentSectionSettingCommand(string SectionType) : IRequest;
public record DeactivateHomeContentSectionSettingCommand(string SectionType) : IRequest;

public class UpdateHomeContentSectionSettingCommandValidator : AbstractValidator<UpdateHomeContentSectionSettingCommand>
{
    public UpdateHomeContentSectionSettingCommandValidator()
    {
        RuleFor(x => x.SectionType).NotEmpty().Must(HomeContentSectionSettingHelpers.IsValidSectionType);
    }
}

public class ActivateHomeContentSectionSettingCommandValidator : AbstractValidator<ActivateHomeContentSectionSettingCommand>
{
    public ActivateHomeContentSectionSettingCommandValidator()
    {
        RuleFor(x => x.SectionType).NotEmpty().Must(HomeContentSectionSettingHelpers.IsValidSectionType);
    }
}

public class DeactivateHomeContentSectionSettingCommandValidator : AbstractValidator<DeactivateHomeContentSectionSettingCommand>
{
    public DeactivateHomeContentSectionSettingCommandValidator()
    {
        RuleFor(x => x.SectionType).NotEmpty().Must(HomeContentSectionSettingHelpers.IsValidSectionType);
    }
}

public class UpdateHomeContentSectionSettingCommandHandler : IRequestHandler<UpdateHomeContentSectionSettingCommand, HomeContentSectionSettingDto>
{
    private readonly IApplicationDbContext _context;
    public UpdateHomeContentSectionSettingCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<HomeContentSectionSettingDto> Handle(UpdateHomeContentSectionSettingCommand request, CancellationToken cancellationToken)
    {
        var sectionType = HomeContentSectionSettingHelpers.ParseSectionType(request.SectionType);
        var entity = await _context.HomeContentSectionSettings
            .FirstOrDefaultAsync(x => x.SectionType == sectionType, cancellationToken);

        if (entity is null)
        {
            entity = new HomeContentSectionSetting(sectionType, request.IsEnabled);
            _context.HomeContentSectionSettings.Add(entity);
        }
        else
        {
            entity.SetEnabled(request.IsEnabled);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new HomeContentSectionSettingDto(entity.SectionType.ToString(), entity.IsEnabled);
    }
}

public class ActivateHomeContentSectionSettingCommandHandler : IRequestHandler<ActivateHomeContentSectionSettingCommand>
{
    private readonly IApplicationDbContext _context;
    public ActivateHomeContentSectionSettingCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(ActivateHomeContentSectionSettingCommand request, CancellationToken cancellationToken)
    {
        var sectionType = HomeContentSectionSettingHelpers.ParseSectionType(request.SectionType);
        var entity = await _context.HomeContentSectionSettings
            .FirstOrDefaultAsync(x => x.SectionType == sectionType, cancellationToken);

        if (entity is null)
        {
            _context.HomeContentSectionSettings.Add(new HomeContentSectionSetting(sectionType, true));
        }
        else
        {
            entity.Activate();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class DeactivateHomeContentSectionSettingCommandHandler : IRequestHandler<DeactivateHomeContentSectionSettingCommand>
{
    private readonly IApplicationDbContext _context;
    public DeactivateHomeContentSectionSettingCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(DeactivateHomeContentSectionSettingCommand request, CancellationToken cancellationToken)
    {
        var sectionType = HomeContentSectionSettingHelpers.ParseSectionType(request.SectionType);
        var entity = await _context.HomeContentSectionSettings
            .FirstOrDefaultAsync(x => x.SectionType == sectionType, cancellationToken);

        if (entity is null)
        {
            _context.HomeContentSectionSettings.Add(new HomeContentSectionSetting(sectionType, false));
        }
        else
        {
            entity.Deactivate();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

internal static class HomeContentSectionSettingHelpers
{
    public static bool IsValidSectionType(string value) =>
        Enum.TryParse<HomeContentSectionType>(value, true, out _);

    public static HomeContentSectionType ParseSectionType(string value)
    {
        if (!Enum.TryParse<HomeContentSectionType>(value, true, out var parsed))
        {
            throw new BusinessRuleException("INVALID_HOME_CONTENT_SECTION_TYPE", "Invalid home content section type.");
        }

        return parsed;
    }
}
