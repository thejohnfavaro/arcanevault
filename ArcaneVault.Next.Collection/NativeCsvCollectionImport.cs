using System.Text;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Read-only UTF-8/UTF-8-BOM (also UTF-16 BOM) CSV physical inventory parser.
/// Maps onto the existing Excel row model so all catalog verification, preview,
/// additive-import, rollback and stale-SHA protections remain identical.
/// </summary>
public static class NativeCsvCollectionImport
{
    private const int MaxRows = 60_000;
    private const int MaxColumns = 64;
    private const int MaxFieldLength = 16_384;
    private sealed record Record(int Line, string[] Fields);

    private static StreamReader Reader(string path) => new(
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read),
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true),
        detectEncodingFromByteOrderMarks: true, bufferSize: 65_536);

    private static char DetectSeparator(string path)
    {
        using var reader = Reader(path);
        bool quoted = false;
        int comma = 0, semi = 0, examined = 0, lines = 0;
        while (examined++ < 32_768 && reader.Read() is int code && code >= 0)
        {
            char c = (char)code;
            if (c == '"')
            {
                if (quoted && reader.Peek() == '"')
                {
                    reader.Read();
                    examined++;
                }
                else quoted = !quoted;
                continue;
            }
            if (quoted) continue;
            if (c == ',') comma++;
            else if (c == ';') semi++;
            else if (c == '\r' || c == '\n')
            {
                // Use the first delimited header-like line, skipping optional
                // export banners/preamble.
                if (comma > 0 || semi > 0)
                    return semi > comma ? ';' : ',';
                if (++lines >= 25) break;
                comma = 0;
                semi = 0;
            }
        }
        return semi > comma ? ';' : ',';
    }

    /// <summary>
    /// RFC 4180 double-quoted fields (including embedded newlines, commas and
    /// escaped quotes). Consumes data once, caps fields/columns and rejects
    /// unmatched quotes instead of silently shifting quantity columns.
    /// </summary>
    private static IEnumerable<Record> Records(TextReader reader, char separator)
    {
        int line = 1;
        int recordLine = 1;
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false, quoteClosed = false, hasContent = false;
        int code;
        while ((code = reader.Read()) != -1)
        {
            char c = (char)code;
            hasContent = true;
            if (quoted)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        quoted = false;
                        quoteClosed = true;
                    }
                }
                else
                {
                    field.Append(c);
                    if (c == '\n') line++;
                }
            }
            else if (c == separator)
            {
                fields.Add(field.ToString().Trim());
                if (fields.Count >= MaxColumns)
                    throw new InvalidDataException(
                        $"CSV row {recordLine} exceeds {MaxColumns} columns.");
                field.Clear();
                quoteClosed = false;
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && reader.Peek() == '\n') reader.Read();
                fields.Add(field.ToString().Trim());
                yield return new Record(recordLine, fields.ToArray());
                fields.Clear();
                field.Clear();
                quoteClosed = false;
                hasContent = false;
                line++;
                recordLine = line;
            }
            else if (c == '"')
            {
                if (field.Length != 0 || quoteClosed)
                    throw new InvalidDataException(
                        $"CSV row {recordLine} contains a misplaced quote.");
                quoted = true;
            }
            else
            {
                if (quoteClosed && !char.IsWhiteSpace(c))
                    throw new InvalidDataException(
                        $"CSV row {recordLine} contains text after a closing quote.");
                field.Append(c);
            }

            if (field.Length > MaxFieldLength)
                throw new InvalidDataException(
                    $"CSV row {recordLine} exceeds the {MaxFieldLength}-character field limit.");
        }
        if (quoted)
            throw new InvalidDataException(
                $"CSV row {recordLine} contains an unclosed quoted field.");
        if (hasContent || fields.Count > 0)
        {
            fields.Add(field.ToString().Trim());
            yield return new Record(recordLine, fields.ToArray());
        }
    }

    public static IReadOnlyList<NativeExcelCollectionImport.Row> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists || !info.Extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
            info.Length is < 1 or > 35_000_000)
            throw new InvalidDataException("Choose a .csv collection export under 35 MB.");

        char delimiter = DetectSeparator(path);
        using var reader = Reader(path);
        Dictionary<string, int>? headers = null;
        int seen = 0;
        var result = new List<NativeExcelCollectionImport.Row>();
        foreach (var record in Records(reader, delimiter))
        {
            seen++;
            if (seen > MaxRows + 25)
                throw new InvalidDataException("CSV import exceeds 60,000 card rows.");
            if (record.Fields.Length > MaxColumns)
                throw new InvalidDataException(
                    $"CSV row {record.Line} exceeds {MaxColumns} columns.");
            if (headers is null)
            {
                var map = NativeExcelCollectionImport.Headers(record.Fields);
                if (map.ContainsKey("name") && map.ContainsKey("quantity"))
                {
                    headers = map;
                    continue;
                }
                if (seen >= 25)
                    throw new InvalidDataException(
                        "CSV requires Name and Quantity headers within the first 25 records.");
                continue;
            }
            string name = record.Fields.Length > headers["name"]
                ? record.Fields[headers["name"]] : "";
            string qty = record.Fields.Length > headers["quantity"]
                ? record.Fields[headers["quantity"]] : "";
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(qty))
                continue;
            var parsed = NativeExcelCollectionImport.ParseMappedRow(
                headers, record.Fields, record.Line);
            result.Add(parsed);
            if (result.Count > MaxRows)
                throw new InvalidDataException("CSV import exceeds 60,000 card rows.");
        }
        if (headers is null || result.Count == 0)
            throw new InvalidDataException(
                "CSV contains no importable physical collection rows.");
        return result.AsReadOnly();
    }
}
