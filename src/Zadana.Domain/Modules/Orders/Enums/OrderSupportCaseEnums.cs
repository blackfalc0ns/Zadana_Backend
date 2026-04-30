namespace Zadana.Domain.Modules.Orders.Enums;

public enum OrderSupportCaseType
{
    Complaint,
    ReturnRequest
}

public enum OrderSupportCaseStatus
{
    Submitted,
    InReview,
    AwaitingCustomerEvidence,
    Approved,
    Rejected,
    Resolved
}

public enum OrderSupportCasePriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum OrderSupportCaseQueue
{
    Support,
    Finance,
    Operations
}
