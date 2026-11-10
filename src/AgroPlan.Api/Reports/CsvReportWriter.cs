using System.Globalization;
using System.Text;

namespace AgroPlan.Api.Reports;

public sealed class CsvReportWriter
{
    public byte[] Write(IEnumerable<IEnumerable<string?>> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
            builder.AppendLine(string.Join(',', row.Select(Escape)));
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    public static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        // Prefix formula-leading cells with an apostrophe, including after leading whitespace.
        var first = text.TrimStart().FirstOrDefault();
        if (first is '=' or '+' or '-' or '@') text = "'" + text;
        return '"' + text.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}
