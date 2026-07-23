using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace Zadana.Application.Common.Export;

public static class ExcelExportBuilder
{
    private const string UnicodeFontName = "Segoe UI";
    private static readonly Regex ArabicRegex = new(@"\p{IsArabic}", RegexOptions.Compiled);

    public static ExportFile Build(
        string fileName,
        string sheetName,
        IReadOnlyList<ExportColumn> columns,
        IEnumerable<IReadOnlyDictionary<string, string?>> rows,
        bool rightToLeft = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(columns));
        }

        var materialisedRows = rows.Take(ExportLimits.MaxRows).ToList();
        var rtl = rightToLeft
            || IsArabicCulture()
            || columns.Any(column => ContainsArabic(column.Header))
            || materialisedRows.Any(row => row.Values.Any(ContainsArabic));

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(sheetName));
        worksheet.RightToLeft = rtl;
        worksheet.Style.Font.FontName = UnicodeFontName;

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = column.Header;
            cell.Style.Font.FontName = UnicodeFontName;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F766E");
            cell.Style.Alignment.Horizontal = rtl
                ? XLAlignmentHorizontalValues.Right
                : XLAlignmentHorizontalValues.Left;

            if (column.Width is > 0)
            {
                worksheet.Column(i + 1).Width = column.Width.Value;
            }
        }

        var rowIndex = 2;
        foreach (var row in materialisedRows)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                row.TryGetValue(columns[i].Key, out var value);
                var cell = worksheet.Cell(rowIndex, i + 1);
                cell.Value = value ?? string.Empty;
                cell.Style.Font.FontName = UnicodeFontName;
                cell.Style.Alignment.Horizontal = rtl
                    ? XLAlignmentHorizontalValues.Right
                    : XLAlignmentHorizontalValues.Left;
            }

            rowIndex++;
        }

        worksheet.SheetView.FreezeRows(1);
        if (columns.All(c => c.Width is null or <= 0))
        {
            worksheet.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var safeName = EnsureExtension(fileName, ".xlsx");
        return new ExportFile(stream.ToArray(), ExportFile.ExcelContentType, safeName);
    }

    public static ExportFile BuildFromObjects<T>(
        string fileName,
        string sheetName,
        IReadOnlyList<ExportColumn> columns,
        IEnumerable<T> items,
        Func<T, IReadOnlyDictionary<string, string?>> mapRow,
        bool rightToLeft = false) =>
        Build(fileName, sheetName, columns, items.Select(mapRow), rightToLeft);

    private static bool IsArabicCulture() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsArabic(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ArabicRegex.IsMatch(value);

    private static string SanitizeSheetName(string sheetName)
    {
        var name = string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars().Concat([':', '\\', '/', '?', '*', '[', ']']))
        {
            name = name.Replace(invalid, '_');
        }

        return name.Length <= 31 ? name : name[..31];
    }

    private static string EnsureExtension(string fileName, string extension)
    {
        var trimmed = fileName.Trim();
        return trimmed.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}{extension}";
    }
}
