namespace Zadana.Domain.Modules.Wallets.Enums;

/// <summary>
/// Identifies the execution channel that currently owns a payout. A payout can
/// only have one active reservation at a time, which prevents an administrator
/// and a gateway worker from sending the same money twice.
/// </summary>
public enum PayoutExecutionMode
{
    Automatic,
    Manual
}
