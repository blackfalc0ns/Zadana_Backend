using FluentValidation;
using MediatR;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorHours;

public record AdminUpdateVendorHoursItem(int DayOfWeek, string OpenTime, string CloseTime, bool IsOpen);

public record AdminUpdateVendorHoursCommand(Guid VendorId, IReadOnlyCollection<AdminUpdateVendorHoursItem> Hours) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorHoursCommandValidator : AbstractValidator<AdminUpdateVendorHoursCommand>
{
    public AdminUpdateVendorHoursCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
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

public class AdminUpdateVendorHoursCommandHandler : IRequestHandler<AdminUpdateVendorHoursCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheInvalidator _cacheInvalidator;

    public AdminUpdateVendorHoursCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork,
        ICacheInvalidator cacheInvalidator)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorHoursCommand request, CancellationToken cancellationToken)
    {
        var requestedHours = ParseRequestedHours(request.Hours);
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var primaryBranch = VendorPrimaryBranchFactory.RequireExistingOrThrow(vendor);

        var replacementHours = BuildReplacementHours(primaryBranch, requestedHours);
        await _vendorRepository.ReplaceBranchOperatingHoursAsync(primaryBranch.Id, replacementHours, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_hours_updated",
                "حدّثنا ساعات تشغيل المتجر",
                "Vendor operating hours updated",
                "حدّثنا ساعات تشغيل المتجر من لوحة الإدارة.",
                "Your store operating hours were updated by the admin team.",
                "/profile",
                vendor.Id),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
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
        IEnumerable<AdminUpdateVendorHoursItem> hours)
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
