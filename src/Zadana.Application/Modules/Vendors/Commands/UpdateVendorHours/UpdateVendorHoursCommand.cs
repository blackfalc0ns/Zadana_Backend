using FluentValidation;
using MediatR;
using Zadana.Application.Common.Caching;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorHours;

public record UpdateVendorHoursItem(int DayOfWeek, string OpenTime, string CloseTime, bool IsOpen);

public record UpdateVendorHoursCommand(IReadOnlyCollection<UpdateVendorHoursItem> Hours) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorHoursCommandValidator : AbstractValidator<UpdateVendorHoursCommand>
{
    public UpdateVendorHoursCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Hours).NotEmpty();
        RuleForEach(x => x.Hours).ChildRules(hour =>
        {
            hour.RuleFor(x => x.DayOfWeek).InclusiveBetween(0, 6);
            hour.RuleFor(x => x.OpenTime)
                .NotEmpty()
                .Must(VendorOperatingHourTimeParser.IsValidClockTime)
                .WithMessage("Open time must use HH:mm format between 00:00 and 23:59.");
            hour.RuleFor(x => x.CloseTime)
                .NotEmpty()
                .Must(VendorOperatingHourTimeParser.IsValidClockTime)
                .WithMessage("Close time must use HH:mm format between 00:00 and 23:59.");
        });
    }
}

public class UpdateVendorHoursCommandHandler : IRequestHandler<UpdateVendorHoursCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateVendorHoursCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IVendorReviewAuditService vendorReviewAuditService,
        ICacheInvalidator cacheInvalidator)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorHoursCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var requestedHours = ParseRequestedHours(request.Hours);
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        var primaryBranch = vendor.Branches
            .OrderByDescending(branch => branch.IsActive)
            .ThenBy(branch => branch.CreatedAtUtc)
            .FirstOrDefault();

        var branchWasCreated = primaryBranch == null;
        if (primaryBranch == null)
        {
            primaryBranch = VendorPrimaryBranchFactory.CreateForHoursProfile(vendor);

            _vendorRepository.AddBranch(primaryBranch);
            vendor.Branches.Add(primaryBranch);
        }

        if (branchWasCreated)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var replacementHours = BuildReplacementHours(primaryBranch, requestedHours);
        await _vendorRepository.ReplaceBranchOperatingHoursAsync(primaryBranch.Id, replacementHours, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "profile-hours-updated",
            "info",
            "تم تحديث ساعات العمل من بوابة التاجر.",
            "بوابة التاجر",
            vendor.BusinessNameAr,
            userId,
            vendor.BusinessNameAr,
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }

    private static IReadOnlyCollection<BranchOperatingHour> BuildReplacementHours(
        VendorBranch branch,
        IReadOnlyCollection<ParsedOperatingHour> requestedHours)
    {
        var hoursByDay = branch.OperatingHours
            .GroupBy(hour => hour.DayOfWeek)
            .Select(group => group.Last())
            .ToDictionary(
                hour => hour.DayOfWeek,
                hour => new ParsedOperatingHour(hour.DayOfWeek, hour.OpenTime, hour.CloseTime, !hour.IsClosed));

        foreach (var requestedHour in requestedHours)
        {
            hoursByDay[requestedHour.DayOfWeek] = requestedHour;
        }

        return hoursByDay.Values
            .OrderBy(hour => hour.DayOfWeek)
            .Select(hour => new BranchOperatingHour(
                branch.Id,
                hour.DayOfWeek,
                hour.OpenTime,
                hour.CloseTime,
                !hour.IsOpen))
            .ToArray();
    }

    private static IReadOnlyCollection<ParsedOperatingHour> ParseRequestedHours(
        IEnumerable<UpdateVendorHoursItem> hours)
    {
        return hours
            .GroupBy(hour => hour.DayOfWeek)
            .Select(group => group.Last())
            .Select(hour => new ParsedOperatingHour(
                hour.DayOfWeek,
                VendorOperatingHourTimeParser.ParseClockTime(hour.OpenTime),
                VendorOperatingHourTimeParser.ParseClockTime(hour.CloseTime),
                hour.IsOpen))
            .ToArray();
    }

    private sealed record ParsedOperatingHour(int DayOfWeek, TimeSpan OpenTime, TimeSpan CloseTime, bool IsOpen);
}
