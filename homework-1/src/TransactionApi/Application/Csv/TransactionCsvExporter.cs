using System.Globalization;
using System.Text;
using TransactionApi.Application.DTOs;

namespace TransactionApi.Application.Csv;

public static class TransactionCsvExporter
{
    private const string NewLine = "\r\n";

    public static string ToCsv(IReadOnlyList<TransactionDto> transactions)
    {
        var sb = new StringBuilder();
        sb.Append("Id,FromAccount,ToAccount,Amount,Currency,Type,Timestamp,Status");
        sb.Append(NewLine);

        foreach (var t in transactions)
        {
            sb.Append(t.Id.ToString());
            sb.Append(',');
            sb.Append(Escape(t.FromAccount));
            sb.Append(',');
            sb.Append(Escape(t.ToAccount));
            sb.Append(',');
            sb.Append(t.Amount.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(Escape(t.Currency));
            sb.Append(',');
            sb.Append(Escape(t.Type));
            sb.Append(',');
            sb.Append(t.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(Escape(t.Status));
            sb.Append(NewLine);
        }

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.IndexOfAny([',', '\"', '\r', '\n']) < 0)
            return value;

        return '\"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '\"';
    }
}
