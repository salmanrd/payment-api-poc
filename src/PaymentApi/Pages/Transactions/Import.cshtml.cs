using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace PaymentApi.Pages.Transactions;

public sealed class ImportModel(PaymentDbContext database, ILogger<ImportModel> logger) : PageModel
{
    public const int MaximumRows = 100;

    private static readonly string[] RequiredHeaders =
    [
        "TransactionId", "CaseNo", "TransactionType", "TransactionMethodId",
        "TransactionDate", "Amount", "TransactionStatus", "PaymentReference"
    ];

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    public int? ImportedCount { get; private set; }

    public void OnGet(int? imported) => ImportedCount = imported;

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        if (CsvFile is null || CsvFile.Length == 0)
        {
            ModelState.AddModelError(nameof(CsvFile), "Select a non-empty CSV file.");
            return Page();
        }

        if (!string.Equals(Path.GetExtension(CsvFile.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(CsvFile), "The selected file must have a .csv extension.");
            return Page();
        }

        List<TransactionEntity> rows;
        try
        {
            await using var stream = CsvFile.OpenReadStream();
            rows = ParseCsv(stream);
        }
        catch (CsvImportException exception)
        {
            ModelState.AddModelError(nameof(CsvFile), exception.Message);
            return Page();
        }

        var duplicateIds = rows.GroupBy(row => row.TransactionId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            ModelState.AddModelError(nameof(CsvFile), $"The CSV contains a duplicate transaction ID: {duplicateIds[0]}.");
            return Page();
        }

        var ids = rows.Select(row => row.TransactionId).ToArray();
        var existingId = await database.Transactions.AsNoTracking()
            .Where(transaction => ids.Contains(transaction.TransactionId))
            .Select(transaction => (Guid?)transaction.TransactionId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingId is not null)
        {
            ModelState.AddModelError(nameof(CsvFile), $"Transaction {existingId} already exists. No rows were imported.");
            return Page();
        }

        try
        {
            database.Transactions.AddRange(rows);
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Unable to import transactions from {FileName}", CsvFile.FileName);
            ModelState.AddModelError(nameof(CsvFile), "The transactions could not be imported. No rows were added.");
            return Page();
        }

        return RedirectToPage(new { imported = rows.Count });
    }

    private static List<TransactionEntity> ParseCsv(Stream stream)
    {
        using var parser = new TextFieldParser(stream);
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = true;

        try
        {
            if (parser.EndOfData) throw new CsvImportException("The CSV file is empty.");
            var headers = parser.ReadFields() ?? [];
            if (headers.Length > 0) headers[0] = headers[0].TrimStart('\uFEFF');
            var positions = headers
                .Select((header, index) => (Header: NormaliseHeader(header), Index: index))
                .GroupBy(item => item.Header)
                .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);

            var missingHeaders = RequiredHeaders.Where(header => !positions.ContainsKey(NormaliseHeader(header))).ToArray();
            if (missingHeaders.Length > 0)
                throw new CsvImportException($"The CSV is missing required column(s): {string.Join(", ", missingHeaders)}.");

            var rows = new List<TransactionEntity>();
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields() ?? [];
                if (fields.All(string.IsNullOrWhiteSpace)) continue;
                if (rows.Count == MaximumRows)
                    throw new CsvImportException($"The CSV contains more than the maximum {MaximumRows} rows. No rows were imported.");

                var rowNumber = rows.Count + 2;
                string Value(string header)
                {
                    var index = positions[NormaliseHeader(header)];
                    return index < fields.Length ? fields[index].Trim() : string.Empty;
                }

                if (!Guid.TryParse(Value("TransactionId"), out var transactionId))
                    throw InvalidValue(rowNumber, "TransactionId");
                if (!TryParseTransactionMethod(Value("TransactionMethodId"), out var methodId))
                    throw InvalidValue(rowNumber, "TransactionMethodId");
                if (!DateTimeOffset.TryParse(Value("TransactionDate"), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var transactionDate))
                    throw InvalidValue(rowNumber, "TransactionDate");
                if (!decimal.TryParse(Value("Amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                    throw InvalidValue(rowNumber, "Amount");

                var caseNo = Required(Value("CaseNo"), rowNumber, "CaseNo");
                var type = Required(Value("TransactionType"), rowNumber, "TransactionType");
                var status = Required(Value("TransactionStatus"), rowNumber, "TransactionStatus");
                var paymentReference = Required(Value("PaymentReference"), rowNumber, "PaymentReference");
                var originalReference = positions.ContainsKey(NormaliseHeader("OriginalPaymentReference"))
                    ? Value("OriginalPaymentReference") : null;

                rows.Add(new TransactionEntity
                {
                    TransactionId = transactionId,
                    CaseNo = caseNo,
                    TransactionType = type,
                    TransactionMethodId = methodId,
                    TransactionDate = transactionDate,
                    Amount = amount,
                    TransactionStatus = status,
                    OriginalPaymentReference = string.IsNullOrWhiteSpace(originalReference) ? null : originalReference,
                    PaymentReference = paymentReference
                });
            }

            if (rows.Count == 0) throw new CsvImportException("The CSV does not contain any transaction rows.");
            return rows;
        }
        catch (MalformedLineException exception)
        {
            throw new CsvImportException($"The CSV is malformed near line {exception.LineNumber}.");
        }
    }

    private static string NormaliseHeader(string header) =>
        new(header.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static bool TryParseTransactionMethod(string value, out int methodId)
    {
        if (string.Equals(value, "Any", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 0;
            return true;
        }

        if (string.Equals(value, "Bank Transfer", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 1;
            return true;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out methodId);
    }

    private static string Required(string value, int rowNumber, string column) =>
        string.IsNullOrWhiteSpace(value) ? throw InvalidValue(rowNumber, column) : value;

    private static CsvImportException InvalidValue(int rowNumber, string column) =>
        new($"Row {rowNumber} has a missing or invalid {column} value. No rows were imported.");

    private sealed class CsvImportException(string message) : Exception(message);
}
