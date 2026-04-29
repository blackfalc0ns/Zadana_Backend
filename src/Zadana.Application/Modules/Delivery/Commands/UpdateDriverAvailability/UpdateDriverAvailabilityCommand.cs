using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.UpdateDriverAvailability;

public record UpdateDriverAvailabilityCommand(Guid DriverUserId, bool IsAvailable) : IRequest;

public class UpdateDriverAvailabilityCommandHandler : IRequestHandler<UpdateDriverAvailabilityCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IDriverCommitmentPolicyService _driverCommitmentPolicyService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDriverAvailabilityCommandHandler(
        IDriverRepository driverRepository,
        IDriverCommitmentPolicyService driverCommitmentPolicyService,
        IUnitOfWork unitOfWork)
    {
        _driverRepository = driverRepository;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateDriverAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverUserId);

        if (request.IsAvailable && !driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "يجب مراجعة حسابك والموافقة عليه قبل تفعيل التوفر | Your account must be approved before you can go online.");
        }

        if (request.IsAvailable)
        {
            var commitmentSummary = await _driverCommitmentPolicyService.GetDriverSummaryAsync(driver.Id, cancellationToken);
            if (!commitmentSummary.CanReceiveOffers)
            {
                throw new BusinessRuleException(
                    "DRIVER_SOFT_BLOCKED_BY_REJECTIONS",
                    commitmentSummary.RestrictionMessage ??
                    "تم تجاوز الحد المسموح به لرفض الطلبات. حاول لاحقاً | You have exceeded the offer rejection limit. Please try again later.");
            }
        }

        driver.ToggleAvailability(request.IsAvailable);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
