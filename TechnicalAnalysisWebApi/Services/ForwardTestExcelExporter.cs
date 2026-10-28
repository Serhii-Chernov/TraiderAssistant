using ClosedXML.Excel;
using TechnicalAnalysis.Domain.Models;

namespace TechnicalAnalysis.WebApi.Services
{
    /// <summary>
    /// Builds Excel workbooks for monthly forward-test results.
    /// </summary>
    public static class ForwardTestExcelExporter
    {
        /// <summary>
        /// Creates an XLSX workbook with a summary sheet and separate sheets for each currency pair.
        /// </summary>
        /// <returns>The XLSX workbook content as a byte array.</returns>
        public static byte[] Export(IEnumerable<ForwardTestMonthlyResult> results)
        {
            var orderedResults = results
                .OrderBy(r => r.Month)
                .ThenBy(r => r.CurrencyPair)
                .ThenBy(r => r.Timeframe)
                .ToList();

            using var workbook = new XLWorkbook();

            AddWorksheet(workbook, "Summary", orderedResults, includeCurrencyPair: true);

            foreach (var pairGroup in orderedResults.GroupBy(r => r.CurrencyPair).OrderBy(g => g.Key))
            {
                AddWorksheet(workbook, GetWorksheetName(pairGroup.Key), pairGroup.ToList(), includeCurrencyPair: false);
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static void AddWorksheet(
            XLWorkbook workbook,
            string sheetName,
            IReadOnlyCollection<ForwardTestMonthlyResult> results,
            bool includeCurrencyPair)
        {
            var worksheet = workbook.Worksheets.Add(sheetName);
            var headers = includeCurrencyPair
                ? new[] { "Month", "CurrencyPair", "Timeframe", "TotalTrades", "WinRate", "MaxDrawdown", "NetProfit" }
                : new[] { "Month", "Timeframe", "TotalTrades", "WinRate", "MaxDrawdown", "NetProfit" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }

            int row = 2;
            foreach (var result in results)
            {
                int column = 1;
                worksheet.Cell(row, column++).Value = result.Month;

                if (includeCurrencyPair)
                {
                    worksheet.Cell(row, column++).Value = result.CurrencyPair;
                }

                worksheet.Cell(row, column++).Value = result.Timeframe;
                worksheet.Cell(row, column++).Value = result.TotalTrades;
                worksheet.Cell(row, column++).Value = result.WinRate;
                worksheet.Cell(row, column++).Value = result.MaxDrawdown;
                worksheet.Cell(row, column).Value = result.NetProfit;
                row++;
            }

            FormatWorksheet(worksheet, headers.Length, Math.Max(row - 1, 1));
        }

        private static void FormatWorksheet(IXLWorksheet worksheet, int columnCount, int lastRow)
        {
            var headerRange = worksheet.Range(1, 1, 1, columnCount);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");

            if (lastRow > 1)
            {
                var dataRange = worksheet.Range(1, 1, lastRow, columnCount);
                dataRange.CreateTable();

                for (int column = 1; column <= columnCount; column++)
                {
                    var header = worksheet.Cell(1, column).GetString();
                    if (header is "WinRate" or "MaxDrawdown" or "NetProfit")
                    {
                        worksheet.Column(column).Style.NumberFormat.Format = "0.00";
                    }
                }
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns().AdjustToContents();
        }

        private static string GetWorksheetName(string currencyPair)
        {
            var invalidChars = new[] { '\\', '/', '*', '[', ']', ':', '?' };
            var cleanedName = invalidChars.Aggregate(currencyPair, (current, invalidChar) => current.Replace(invalidChar, '_'));

            return cleanedName.Length <= 31 ? cleanedName : cleanedName[..31];
        }
    }
}
