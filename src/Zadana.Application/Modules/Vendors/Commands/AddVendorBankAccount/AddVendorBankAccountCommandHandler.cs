using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AddVendorBankAccount;

public class AddVendorBankAccountCommandHandler : IRequestHandler<AddVendorBankAccountCommand, Guid>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IProfileChangeApprovalService _profileChangeApprovalService;

    public AddVendorBankAccountCommandHandler(
        IVendorRepository vendorRepository,
        ICurrentUserService currentUserService,
        IProfileChangeApprovalService profileChangeApprovalService)
    {
        _vendorRepository = vendorRepository;
        _currentUserService = currentUserService;
        _profileChangeApprovalService = profileChangeApprovalService;
    }

    public async Task<Guid> Handle(AddVendorBankAccountCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var requestedByUserId = _currentUserService.UserId ?? vendor.UserId;
        return await _profileChangeApprovalService.SubmitAsync(
            requestedByUserId,
            vendor.UserId,
            ProfileChangeApprovalActions.VendorProfileBanking,
            $"Vendor {vendor.BusinessNameEn} requested a bank account change.",
            new VendorBankingProfileChangePayload(
                vendor.Id,
                request.BankName,
                request.AccountHolderName,
                request.Iban,
                request.SwiftCode,
                vendor.PayoutCycle),
            new ProfileChangeApprovalAlert(
                AdminAlertTypes.VendorCriticalChangeSubmitted,
                AdminAlertCategories.Vendors,
                AdminAlertPriorities.High,
                "تعديل حساب تسويات بانتظار الموافقة",
                "Vendor banking change pending approval",
                $"أرسل التاجر {vendor.BusinessNameAr} تعديل حساب التسويات وينتظر موافقة الأدمن.",
                $"Vendor {vendor.BusinessNameEn} submitted banking changes pending admin approval.",
                vendor.Id,
                "/admin/access/approvals",
                new { vendorId = vendor.Id, userId = vendor.UserId, section = "banking", source = "add_bank_account_command" }),
            cancellationToken);
    }
}
