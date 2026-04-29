using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.SubmitDeliveryProof;

public class SubmitDeliveryProofCommandHandler : IRequestHandler<SubmitDeliveryProofCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitDeliveryProofCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(SubmitDeliveryProofCommand request, CancellationToken cancellationToken)
    {
        var assignment = await _context.DeliveryAssignments
            .FirstOrDefaultAsync(x => x.Id == request.AssignmentId, cancellationToken)
            ?? throw new NotFoundException("DeliveryAssignment", request.AssignmentId);

        if (assignment.Status is not (AssignmentStatus.PickedUp or AssignmentStatus.ArrivedAtCustomer or AssignmentStatus.Accepted))
        {
            throw new BusinessRuleException("INVALID_PROOF_STATE",
                "لا يمكن إرسال إثبات التوصيل إلا للطلبات النشطة | Delivery proof can only be submitted for active assignments.");
        }

        var proof = new DeliveryProof(
            assignment.Id,
            request.ProofType,
            request.ImageUrl,
            request.OtpCode,
            request.RecipientName,
            request.Note);

        _context.DeliveryProofs.Add(proof);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return proof.Id;
    }
}
