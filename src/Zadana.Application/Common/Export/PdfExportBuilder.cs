using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Zadana.Application.Common.Export;

public static class PdfExportBuilder
{
    private const string ArabicFontFamily = "Noto Sans Arabic";
    private const string EmbeddedFontResource =
        "Zadana.Application.Common.Export.Fonts.NotoSansArabic-Regular.ttf";
    private static readonly object FontLock = new();
    private static bool _fontsRegistered;
    private static readonly Regex ArabicRegex = new(@"\p{IsArabic}", RegexOptions.Compiled);

    static PdfExportBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        EnsureFontsRegistered();
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
        EnsureFontsRegistered();
        var materialisedRows = rows?.Take(ExportLimits.MaxRows).ToList() ?? [];
        var rtl = ShouldUseRtl(title, subtitle, footerNote, meta, columns, materialisedRows, totals);

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => ApplyTextStyle(style.FontSize(10)));

                if (rtl)
                {
                    page.ContentFromRightToLeft();
                }

                page.Header().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.DefaultTextStyle(style => ApplyTextStyle(style.FontSize(16)));
                        text.Span(title).FontColor(Colors.Teal.Darken2);
                    });

                    if (!string.IsNullOrWhiteSpace(subtitle))
                    {
                        col.Item().PaddingTop(4).Text(text =>
                        {
                            text.DefaultTextStyle(style => ApplyTextStyle(style.FontSize(11)));
                            text.Span(subtitle).FontColor(Colors.Grey.Darken2);
                        });
                    }
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    if (meta is { Count: > 0 })
                    {
                        foreach (var item in meta)
                        {
                            col.Item().PaddingBottom(4).Text(text =>
                            {
                                text.DefaultTextStyle(ApplyTextStyle);
                                text.Span($"{item.Label}: {item.Value}");
                            });
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
                                        .Text(text =>
                                        {
                                            text.DefaultTextStyle(ApplyTextStyle);
                                            text.Span(column.Header).FontColor(Colors.White);
                                        });
                                }
                            });

                            foreach (var row in materialisedRows)
                            {
                                foreach (var column in columns)
                                {
                                    row.TryGetValue(column.Key, out var value);
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                        .Text(text =>
                                        {
                                            text.DefaultTextStyle(ApplyTextStyle);
                                            text.Span(value ?? string.Empty);
                                        });
                                }
                            }
                        });
                    }

                    if (totals is { Count: > 0 })
                    {
                        col.Item().PaddingTop(12);
                        foreach (var total in totals)
                        {
                            col.Item().PaddingBottom(3).Text(text =>
                            {
                                text.DefaultTextStyle(ApplyTextStyle);
                                text.Span($"{total.Label}: {total.Value}");
                            });
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(footerNote))
                    {
                        col.Item().PaddingTop(16).Text(text =>
                        {
                            text.DefaultTextStyle(style => ApplyTextStyle(style.FontSize(9)));
                            text.Span(footerNote).FontColor(Colors.Grey.Darken1);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(ApplyTextStyle);
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

    private static TextStyle ApplyTextStyle(TextStyle style) =>
        style.FontFamily(ArabicFontFamily, "Segoe UI", "Arial").Weight(FontWeight.Normal);

    private static bool ShouldUseRtl(
        string title,
        string? subtitle,
        string? footerNote,
        IReadOnlyList<ExportKeyValue>? meta,
        IReadOnlyList<ExportColumn>? columns,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        IReadOnlyList<ExportKeyValue>? totals)
    {
        if (IsArabicCulture())
        {
            return true;
        }

        if (ContainsArabic(title) || ContainsArabic(subtitle) || ContainsArabic(footerNote))
        {
            return true;
        }

        if (meta?.Any(item => ContainsArabic(item.Label) || ContainsArabic(item.Value)) == true)
        {
            return true;
        }

        if (columns?.Any(column => ContainsArabic(column.Header)) == true)
        {
            return true;
        }

        if (totals?.Any(item => ContainsArabic(item.Label) || ContainsArabic(item.Value)) == true)
        {
            return true;
        }

        return rows.Any(row => row.Values.Any(ContainsArabic));
    }

    private static bool IsArabicCulture() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsArabic(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ArabicRegex.IsMatch(value);

    private static void EnsureFontsRegistered()
    {
        if (_fontsRegistered)
        {
            return;
        }

        lock (FontLock)
        {
            if (_fontsRegistered)
            {
                return;
            }

            if (!TryRegisterEmbeddedFont() && !TryRegisterFileFont())
            {
                // Fall back to environment fonts (Segoe UI / Arial) which usually include Arabic on Windows.
            }

            _fontsRegistered = true;
        }
    }

    private static bool TryRegisterEmbeddedFont()
    {
        var assembly = typeof(PdfExportBuilder).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedFontResource);
        if (stream is null)
        {
            return false;
        }

        FontManager.RegisterFontWithCustomName(ArabicFontFamily, stream);
        return true;
    }

    private static bool TryRegisterFileFont()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Common", "Export", "Fonts", "NotoSansArabic-Regular.ttf"),
            Path.Combine(AppContext.BaseDirectory, "Fonts", "NotoSansArabic-Regular.ttf"),
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory,
                "Common",
                "Export",
                "Fonts",
                "NotoSansArabic-Regular.ttf")
        };

        foreach (var fontPath in candidates)
        {
            if (!File.Exists(fontPath))
            {
                continue;
            }

            using var stream = File.OpenRead(fontPath);
            FontManager.RegisterFontWithCustomName(ArabicFontFamily, stream);
            return true;
        }

        return false;
    }

    private static string EnsureExtension(string fileName, string extension)
    {
        var trimmed = fileName.Trim();
        return trimmed.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}{extension}";
    }
}
