using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Authorization;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Identity.Constants;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/finances/statements")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminFinanceStatementsController(
    IApplicationDbContext context) : ApiControllerBase
{
    [HttpGet("summary")]
    [RequireAccess(PermissionKeys.Admin.FinancesView)]
    public async Task<ActionResult<AdminFinanceStatementSummaryDto>> GetSummary(
        [FromQuery] string period = "month",
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var (start, end, label) = ResolvePeriod(period, now);

        var statementLines = await context.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.PostedAtUtc >= start &&
                line.JournalEntry.PostedAtUtc < end)
            .Where(line =>
                line.AccountCode == FinancialAccountCode.PlatformRevenue ||
                line.AccountCode == FinancialAccountCode.RefundExpense ||
                line.AccountCode == FinancialAccountCode.GatewayFeeExpense ||
                line.AccountCode == FinancialAccountCode.TaxPayable)
            .Select(line => new
            {
                line.AccountCode,
                line.DebitAmount,
                line.CreditAmount
            })
            .ToListAsync(cancellationToken);

        var revenue = statementLines
            .Where(line => line.AccountCode == FinancialAccountCode.PlatformRevenue)
            .Sum(line => line.CreditAmount);

        var expenses = statementLines
            .Where(line =>
                line.AccountCode == FinancialAccountCode.RefundExpense ||
                line.AccountCode == FinancialAccountCode.GatewayFeeExpense)
            .Sum(line => line.DebitAmount);

        var vatPayable = statementLines
            .Where(line => line.AccountCode == FinancialAccountCode.TaxPayable)
            .Sum(line => line.CreditAmount);

        var netIncome = revenue - expenses - vatPayable;

        return Ok(new AdminFinanceStatementSummaryDto(
            Math.Round(revenue, 2),
            Math.Round(expenses, 2),
            Math.Round(vatPayable, 2),
            Math.Round(netIncome, 2),
            label));
    }

    private static (DateTime Start, DateTime End, string Label) ResolvePeriod(string period, DateTime now)
    {
        var normalized = period?.Trim().ToLowerInvariant() ?? "month";
        DateTime start;
        DateTime end;
        string label;

        switch (normalized)
        {
            case "today":
                start = now.Date;
                end = start.AddDays(1);
                label = start.ToString("yyyy-MM-dd");
                break;
            case "week":
                start = now.Date.AddDays(-7);
                end = now;
                label = $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}";
                break;
            case "quarter":
                start = now.Date.AddDays(-90);
                end = now;
                label = $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}";
                break;
            default:
                start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddMonths(1);
                label = $"{start:yyyy-MM}";
                break;
        }

        return (start, end, label);
    }
}

public sealed record AdminFinanceStatementSummaryDto(
    decimal Revenue,
    decimal Expenses,
    decimal VatPayable,
    decimal NetIncome,
    string PeriodLabel);
