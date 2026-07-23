using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Zadana.Application.Common.Export;

public static class PdfExportBuilder
{
    static PdfExportBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static ExportFile BuildStatement(
        string fileName,
        string title,
        string? subtitle = null,
        IReadOnlyList<ExportKeyValue>? meta = null,
        IReadOnlyList<ExportColumn>? columns = null,
        IEnumerable<IReadOnlyDictionary<string, string?>>? rows = null,
        IReadOnlyList<ExportKeyValue>? totals = null,
        string? footerNote = null)
    {
        var materialisedRows = rows?.Take(ExportLimits.MaxRows).ToList() ?? [];

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).SemiBold().FontSize(16).FontColor(Colors.Teal.Darken2);
                    if (!string.IsNullOrWhiteSpace(subtitle))
                    {
                        col.Item().PaddingTop(4).Text(subtitle).FontSize(11).FontColor(Colors.Grey.Darken2);
                    }
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    if (meta is { Count: > 0 })
                    {
                        foreach (var item in meta)
                        {
                            col.Item().PaddingBottom(4).Text($"{item.Label}: {item.Value}");
                        }

                        col.Item().PaddingBottom(8);
                    }

                    if (columns is { Count: > 0 } && materialisedRows.Count > 0)
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(defs =>
                            {
                                foreach (var _ in columns)
                                {
                                    defs.RelativeColumn();
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (var column in columns)
                                {
                                    header.Cell().Background(Colors.Teal.Darken2).Padding(4)
                                        .Text(column.Header).FontColor(Colors.White).SemiBold();
                                }
                            });

                            foreach (var row in materialisedRows)
                            {
                                foreach (var column in columns)
                                {
                                    row.TryGetValue(column.Key, out var value);
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                        .Text(value ?? string.Empty);
                                }
                            }
                        });
                    }

                    if (totals is { Count: > 0 })
                    {
                        col.Item().PaddingTop(12);
                        foreach (var total in totals)
                        {
                            col.Item().PaddingBottom(3).Text($"{total.Label}: {total.Value}").SemiBold();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(footerNote))
                    {
                        col.Item().PaddingTop(16).Text(footerNote).FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Zadana · ");
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")).FontColor(Colors.Grey.Medium);
                    text.Span(" UTC");
                });
            });
        }).GeneratePdf();

        return new ExportFile(bytes, ExportFile.PdfContentType, EnsureExtension(fileName, ".pdf"));
    }

    public static ExportFile BuildReceipt(
        string fileName,
        string title,
        IReadOnlyList<ExportKeyValue> fields,
        string? footerNote = null) =>
        BuildStatement(
            fileName,
            title,
            meta: fields,
            footerNote: footerNote);

    private static string EnsureExtension(string fileName, string extension)
    {
        var trimmed = fileName.Trim();
        return trimmed.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}{extension}";
    }
}
