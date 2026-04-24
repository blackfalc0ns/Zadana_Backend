using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
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
            hour.RuleFor(x => x.OpenTime).NotEmpty().MaximumLength(5);
            hour.RuleFor(x => x.CloseTime).NotEmpty().MaximumLength(5);
        });
    }
}

public class AdminUpdateVendorHoursCommandHandler : IRequestHandler<AdminUpdateVendorHoursCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminUpdateVendorHoursCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorHoursCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var primaryBranch = vendor.Branches
            .OrderByDescending(branch => branch.IsActive)
            .ThenBy(branch => branch.CreatedAtUtc)
            .FirstOrDefault();

        if (primaryBranch == null)
        {
            primaryBranch = new VendorBranch(
                vendor.Id,
                vendor.BusinessNameAr,
                vendor.NationalAddress ?? "Primary branch",
                0,
                0,
                vendor.ContactPhone,
                5);

            _vendorRepository.AddBranch(primaryBranch);
            vendor.Branches.Add(primaryBranch);
        }

        foreach (var hour in request.Hours)
        {
            var parsedOpen = TimeSpan.Parse(hour.OpenTime);
            var parsedClose = TimeSpan.Parse(hour.CloseTime);
            var existingHour = primaryBranch.OperatingHours.FirstOrDefault(item => item.DayOfWeek == hour.DayOfWeek);

            if (existingHour == null)
            {
                primaryBranch.OperatingHours.Add(new BranchOperatingHour(
                    primaryBranch.Id,
                    hour.DayOfWeek,
                    parsedOpen,
                    parsedClose,
                    !hour.IsOpen));
            }
            else
            {
                existingHour.Update(parsedOpen, parsedClose, !hour.IsOpen);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_hours_updated",
                "تم تحديث ساعات تشغيل المتجر",
                "Vendor operating hours updated",
                "تم تحديث ساعات تشغيل المتجر من لوحة الإدارة.",
                "Your store operating hours were updated by the admin team.",
                "/profile",
                vendor.Id),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
