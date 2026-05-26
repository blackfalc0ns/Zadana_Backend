using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260524120000_AddWalletTransactionJournalLineReferenceIndex")]
public partial class AddWalletTransactionJournalLineReferenceIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID('tempdb..#DuplicateJournalLineTransactions') IS NOT NULL
                DROP TABLE #DuplicateJournalLineTransactions;

            SELECT
                Id,
                WalletId,
                Amount,
                Direction,
                TxnType
            INTO #DuplicateJournalLineTransactions
            FROM
            (
                SELECT
                    Id,
                    WalletId,
                    Amount,
                    Direction,
                    TxnType,
                    ROW_NUMBER() OVER (
                        PARTITION BY ReferenceType, ReferenceId
                        ORDER BY CreatedAtUtc, Id
                    ) AS RowNumber
                FROM [WalletTransactions]
                WHERE ReferenceType = 'JournalLine'
                    AND ReferenceId IS NOT NULL
            ) duplicate
            WHERE duplicate.RowNumber > 1;

            UPDATE wallet
            SET
                CurrentBalance = wallet.CurrentBalance + adjustments.CurrentBalanceAdjustment,
                CodOwedBalance = wallet.CodOwedBalance + adjustments.CodOwedBalanceAdjustment
            FROM [Wallet] wallet
            INNER JOIN
            (
                SELECT
                    WalletId,
                    SUM(CASE
                        WHEN TxnType <> 'CashCollected' AND Direction = 'IN' THEN -Amount
                        WHEN TxnType <> 'CashCollected' AND Direction = 'OUT' THEN Amount
                        ELSE 0
                    END) AS CurrentBalanceAdjustment,
                    SUM(CASE
                        WHEN TxnType = 'CashCollected' AND Direction = 'OUT' THEN -Amount
                        WHEN TxnType = 'CashCollected' AND Direction = 'IN' THEN Amount
                        ELSE 0
                    END) AS CodOwedBalanceAdjustment
                FROM #DuplicateJournalLineTransactions
                GROUP BY WalletId
            ) adjustments ON adjustments.WalletId = wallet.Id;

            DELETE txn
            FROM [WalletTransactions] txn
            INNER JOIN #DuplicateJournalLineTransactions duplicate
                ON duplicate.Id = txn.Id;

            DROP TABLE #DuplicateJournalLineTransactions;
            """);

        migrationBuilder.CreateIndex(
            name: "UX_WalletTransactions_JournalLineReference",
            table: "WalletTransactions",
            columns: new[] { "ReferenceType", "ReferenceId" },
            unique: true,
            filter: "[ReferenceType] = 'JournalLine' AND [ReferenceId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_WalletTransactions_JournalLineReference",
            table: "WalletTransactions");
    }
}
