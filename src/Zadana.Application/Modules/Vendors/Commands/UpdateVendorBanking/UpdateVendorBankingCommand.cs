using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorBanking;

public record UpdateVendorBankingCommand(
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    string? PayoutCycle) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorBankingCommandValidator : AbstractValidator<UpdateVendorBankingCommand>
{
    public UpdateVendorBankingCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountHolderName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Iban).NotEmpty().MaximumLength(34);
        RuleFor(x => x.SwiftCode).MaximumLength(11);
        RuleFor(x => x.PayoutCycle).MaximumLength(50);
    }
}

public class UpdateVendorBankingCommandHandler : IRequestHandler<UpdateVendorBankingCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateVendorBankingCommandHandler(
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

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorBankingCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        vendor.UpdateBanking(request.PayoutCycle);

        foreach (var account in vendor.BankAccounts)
        {
            account.UnsetPrimary();
        }

        var primaryAccount = vendor.BankAccounts
            .OrderByDescending(account => account.IsPrimary)
            .ThenBy(account => account.CreatedAtUtc)
            .FirstOrDefault();

        if (primaryAccount == null)
        {
            primaryAccount = new VendorBankAccount(
                vendor.Id,
                request.BankName,
                request.AccountHolderName,
                request.Iban,
                request.SwiftCode);

            primaryAccount.MarkAsPreferredForSetup();
            _vendorRepository.AddBankAccount(primaryAccount);
        }
        else
        {
            primaryAccount.UpdateDetails(request.BankName, request.AccountHolderName, request.Iban, request.SwiftCode);
            primaryAccount.MarkAsPreferredForSetup();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
