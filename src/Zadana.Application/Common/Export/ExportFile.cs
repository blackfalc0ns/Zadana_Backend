namespace Zadana.Application.Common.Export;

public static class ExportLimits
{
    public const int MaxRows = 5000;
}

public sealed record ExportColumn(string Header, string Key, double? Width = null);

public sealed record ExportKeyValue(string Label, string Value);

public sealed record ExportFile(byte[] Bytes, string ContentType, string FileName)
{
    public static string ExcelContentType =>
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static string PdfContentType => "application/pdf";
}
