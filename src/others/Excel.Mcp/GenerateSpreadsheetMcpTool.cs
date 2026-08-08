using System.ComponentModel;
using ClosedXML.Excel;
using ModelContextProtocol.Server;

namespace Excel.Mcp;

/// <summary>
/// Generic spreadsheet (.xlsx) üretim tool'u (ClosedXML). Domain bilmez — çağıran satır/sütun verir.
/// Sonuç base64 .xlsx; mail eki (Mail.Mcp send_email) olarak taşınabilir. Kalıcılık YOK.
/// </summary>
[McpServerToolType]
public static class GenerateSpreadsheetMcpTool
{
    public class GenerateSpreadsheetResult
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentBase64 { get; set; } = string.Empty;
        public string ContentType { get; set; } =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }

    [McpServerTool(Name = "generate_spreadsheet")]
    [Description("Verilen sütun başlıkları ve satırlardan bir .xlsx dosyası üretir; base64 içerik döner. " +
                 "Domain bilmez — tablo verisi çağıran tarafından sağlanır.")]
    public static GenerateSpreadsheetResult GenerateSpreadsheet(
        [Description("Sayfa (worksheet) adı")] string sheetName,
        [Description("Sütun başlıkları")] string[] columns,
        [Description("Satırlar — her satır hücre değerleri dizisi (sütun sırasıyla)")] string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var safeSheetName = string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName;
        var sheet = workbook.Worksheets.Add(safeSheetName);

        // Başlık satırı.
        for (var c = 0; c < columns.Length; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = columns[c];
            cell.Style.Font.Bold = true;
        }

        // Veri satırları.
        for (var r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            for (var c = 0; c < row.Length; c++)
                sheet.Cell(r + 2, c + 1).Value = row[c];
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new GenerateSpreadsheetResult
        {
            FileName = $"{safeSheetName}.xlsx",
            ContentBase64 = Convert.ToBase64String(stream.ToArray())
        };
    }
}