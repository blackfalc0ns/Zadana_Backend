using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DeliveryProof : BaseEntity
{
    public Guid AssignmentId { get; private set; }
    public string ProofType { get; private set; } = null!; // E.g. "Signature", "Photo", "OTP"
    public string? ImageUrl { get; private set; }
    public string? OtpCode { get; private set; }
    public string? RecipientName { get; private set; }
    public string? Note { get; private set; }

    // Navigation
    public DeliveryAssignment Assignment { get; private set; } = null!;

    private DeliveryProof() { }

    public DeliveryProof(
        Guid assignmentId, 
        string proofType, 
        string? imageUrl = null, 
        string? otpCode = null, 
        string? recipientName = null, 
        string? note = null)
    {
        AssignmentId = assignmentId;
        ProofType = proofType.Trim();
        ImageUrl = imageUrl?.Trim();
        OtpCode = otpCode?.Trim();
        RecipientName = recipientName?.Trim();
        Note = note?.Trim();
    }
}
