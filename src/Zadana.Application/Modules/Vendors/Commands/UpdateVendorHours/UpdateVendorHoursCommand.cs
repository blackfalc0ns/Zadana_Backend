using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
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
            hour.RuleFor(x => x.OpenTime).NotEmpty().MaximumLength(5);
            hour.RuleFor(x => x.CloseTime).NotEmpty().MaximumLength(5);
        });
    }
}

public class UpdateVendorHoursCommandHandler : IRequestHandler<UpdateVendorHoursCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateVendorHoursCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorHoursCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

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

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
