using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using MultiClinica.API.Common;

namespace MultiClinica.API.Services;

internal sealed record PatientImportSourceRow(
    int Row,
    string? Name,
    string? Email,
    string? CPF,
    string? Rg,
    string? Rua,
    string? Numero,
    string? Bairro,
    string? Cidade,
    string? Estado,
    string? Cep,
    string? Phone);

internal sealed class PatientImportFileParser
{
    private const long MaxExpandedPackageBytes = 50_000_000;
    private const long MaxXmlEntryBytes = MaxExpandedPackageBytes;
    private const long MaxXmlCharacters = 20_000_000;
    private static readonly XNamespace SheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly string[] AllowedHeaders =
    ["Name", "Email", "CPF", "Rg", "Rua", "Numero", "Bairro", "Cidade", "Estado", "Cep", "Phone"];

    public IReadOnlyList<PatientImportSourceRow> Parse(string fileName, byte[] content, int maxRows)
    {
        var extension = Path.GetExtension(fileName);
        var table = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ReadCsv(content, maxRows)
            : extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? ReadXlsx(content, maxRows)
                : throw new PatientImportParseException(ErrorCodes.InvalidFileType, "Envie um arquivo .xlsx ou .csv.");

        return MapRows(table, maxRows);
    }

    private static IReadOnlyList<PatientImportSourceRow> MapRows(IReadOnlyList<CsvRecord> records, int maxRows)
    {
        if (records.Count == 0 || records[0].Cells.All(string.IsNullOrWhiteSpace))
            throw InvalidFile("A primeira linha deve conter os cabeçalhos.");

        var headers = records[0].Cells.Select(header => header.Trim()).ToArray();
        if (headers.Any(string.IsNullOrEmpty))
            throw InvalidFile("Há um cabeçalho vazio. Remova colunas sem nome.");

        var normalizedHeaders = headers.Select(header => header.ToLowerInvariant()).ToArray();
        if (normalizedHeaders.Distinct(StringComparer.Ordinal).Count() != normalizedHeaders.Length)
            throw InvalidFile("Há cabeçalhos duplicados após remover espaços e diferenças de caixa.");

        var allowedHeaders = AllowedHeaders.ToDictionary(header => header, header => header, StringComparer.OrdinalIgnoreCase);
        var unknownHeaders = headers.Where(header => !allowedHeaders.ContainsKey(header)).ToArray();
        if (unknownHeaders.Length > 0)
            throw InvalidFile($"Cabeçalho não permitido: {string.Join(", ", unknownHeaders)}.");

        if (!headers.Contains("Name", StringComparer.OrdinalIgnoreCase))
            throw InvalidFile("A coluna Name é obrigatória.");

        var rows = new List<PatientImportSourceRow>();
        foreach (var record in records.Skip(1))
        {
            if (record.Cells.All(string.IsNullOrWhiteSpace))
                continue;
            if (record.Cells.Length != headers.Length)
                throw InvalidFile($"A estrutura da linha {record.Line} não corresponde aos cabeçalhos.");

            rows.Add(new PatientImportSourceRow(
                record.Line,
                Value(record, headers, "Name"),
                Value(record, headers, "Email"),
                Value(record, headers, "CPF"),
                Value(record, headers, "Rg"),
                Value(record, headers, "Rua"),
                Value(record, headers, "Numero"),
                Value(record, headers, "Bairro"),
                Value(record, headers, "Cidade"),
                Value(record, headers, "Estado"),
                Value(record, headers, "Cep"),
                Value(record, headers, "Phone")));

            if (rows.Count > maxRows)
                throw TooManyRows(maxRows);
        }

        return rows;
    }

    private static string? Value(CsvRecord record, IReadOnlyList<string> headers, string name)
    {
        var index = -1;
        for (var i = 0; i < headers.Count; i++)
        {
            if (headers[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0 || string.IsNullOrWhiteSpace(record.Cells[index]))
            return null;
        return record.Cells[index].Trim();
    }

    private static IReadOnlyList<CsvRecord> ReadCsv(byte[] bytes, int maxRows)
    {
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw InvalidFile("O CSV deve estar codificado em UTF-8.");
        }

        if (text.StartsWith('\uFEFF'))
            text = text[1..];
        if (string.IsNullOrWhiteSpace(text))
            throw InvalidFile("O arquivo está vazio.");

        var delimiter = DetectDelimiter(text);
        var records = new List<CsvRecord>();
        var cells = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var afterQuote = false;
        var line = 1;
        var recordLine = 1;

        void CompleteRecord()
        {
            cells.Add(field.ToString());
            if (cells.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                records.Add(new CsvRecord(recordLine, cells.ToArray()));
                if (records.Count - 1 > maxRows)
                    throw TooManyRows(maxRows);
            }
            cells.Clear();
            field.Clear();
            afterQuote = false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterQuote = true;
                    }
                }
                else
                {
                    field.Append(current);
                    if (current == '\n' || current == '\r' && (index + 1 >= text.Length || text[index + 1] != '\n'))
                        line++;
                    else if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        field.Append(text[++index]);
                        line++;
                    }
                }
                continue;
            }

            if (afterQuote && current is not (' ' or '\t' or '\r' or '\n') && current != delimiter)
                throw InvalidFile($"O CSV contém uma aspa inválida na linha {line}.");
            if (afterQuote && current is ' ' or '\t')
                continue;

            if (current == '"')
            {
                if (field.Length > 0)
                    throw InvalidFile($"O CSV contém uma aspa inválida na linha {line}.");
                inQuotes = true;
            }
            else if (current == delimiter)
            {
                cells.Add(field.ToString());
                field.Clear();
                afterQuote = false;
            }
            else if (current is '\r' or '\n')
            {
                CompleteRecord();
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    index++;
                line++;
                recordLine = line;
            }
            else
            {
                field.Append(current);
            }
        }

        if (inQuotes)
            throw InvalidFile("O CSV termina antes do fechamento de um campo entre aspas.");
        if (field.Length > 0 || cells.Count > 0)
            CompleteRecord();
        return records;
    }

    private static char DetectDelimiter(string text)
    {
        var commas = 0;
        var semicolons = 0;
        var inQuotes = false;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                    index++;
                else
                    inQuotes = !inQuotes;
            }
            else if (!inQuotes && current is '\r' or '\n')
                break;
            else if (!inQuotes && current == ',')
                commas++;
            else if (!inQuotes && current == ';')
                semicolons++;
        }

        if (commas == semicolons && commas > 0)
            throw InvalidFile("Não foi possível identificar o delimitador do CSV.");
        return commas > semicolons ? ',' : ';';
    }

    private static IReadOnlyList<CsvRecord> ReadXlsx(byte[] bytes, int maxRows)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count > 1000)
                throw InvalidFile("O XLSX contém estruturas acima do limite permitido.");
            long expandedPackageBytes = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > MaxExpandedPackageBytes - expandedPackageBytes)
                    throw InvalidFile("O XLSX expandido excede o limite permitido.");
                expandedPackageBytes += entry.Length;
            }

            var workbook = LoadXml(archive, "xl/workbook.xml");
            var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
            var sharedStrings = TryLoadXml(archive, "xl/sharedStrings.xml")?
                .Root?.Elements(SheetNamespace + "si")
                .Select(item => string.Concat(item.Descendants(SheetNamespace + "t").Select(text => text.Value)))
                .ToArray() ?? [];

            var relationshipTargets = relationships.Root?
                .Elements(PackageRelationshipNamespace + "Relationship")
                .Where(item => (string?)item.Attribute("TargetMode") != "External")
                .ToDictionary(
                    item => (string?)item.Attribute("Id") ?? string.Empty,
                    item => ResolveWorksheetTarget((string?)item.Attribute("Target")),
                    StringComparer.Ordinal)
                ?? throw InvalidFile("O XLSX não contém relações de planilha válidas.");

            var sheets = workbook.Root?.Element(SheetNamespace + "sheets")?.Elements(SheetNamespace + "sheet")
                .Select(sheet =>
                {
                    var relationshipId = (string?)sheet.Attribute(RelationshipNamespace + "id");
                    if (relationshipId is null || !relationshipTargets.TryGetValue(relationshipId, out var target))
                        throw InvalidFile("O XLSX contém uma planilha sem destino válido.");
                    var document = LoadXml(archive, target);
                    return ReadSheetRows(document, sharedStrings, maxRows);
                })
                .Where(rows => rows.Count > 0)
                .ToArray();

            if (sheets is not { Length: 1 })
                throw InvalidFile("O XLSX deve conter exatamente uma planilha de dados não vazia.");
            return sheets[0];
        }
        catch (PatientImportParseException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or XmlException or ArgumentException or FormatException or OverflowException)
        {
            throw InvalidFile("O XLSX está corrompido ou possui uma estrutura inválida.");
        }
    }

    private static IReadOnlyList<CsvRecord> ReadSheetRows(
        XDocument document,
        IReadOnlyList<string> sharedStrings,
        int maxRows)
    {
        var sheetData = document.Root?.Element(SheetNamespace + "sheetData")
            ?? throw InvalidFile("O XLSX não contém dados de planilha.");
        var rows = new List<CsvRecord>();
        var fallbackRow = 0;
        foreach (var row in sheetData.Elements(SheetNamespace + "row"))
        {
            var rowNumber = int.TryParse((string?)row.Attribute("r"), out var parsedRow) ? parsedRow : fallbackRow + 1;
            fallbackRow = rowNumber;
            var values = new SortedDictionary<int, string>();
            var fallbackColumn = 0;
            foreach (var cell in row.Elements(SheetNamespace + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                var column = reference is null ? fallbackColumn + 1 : ColumnNumber(reference);
                fallbackColumn = column;
                if (column > 100)
                    throw InvalidFile("O XLSX possui mais colunas que o permitido.");
                values[column] = CellValue(cell, sharedStrings);
            }

            if (values.Count == 0 || values.Values.All(string.IsNullOrWhiteSpace))
                continue;
            var cells = Enumerable.Range(1, values.Keys.Max())
                .Select(column => values.GetValueOrDefault(column, string.Empty))
                .ToArray();
            rows.Add(new CsvRecord(rowNumber, cells));
            if (rows.Count - 1 > maxRows)
                throw TooManyRows(maxRows);
        }

        if (rows.Count > 0)
        {
            var headerColumnCount = rows[0].Cells.Length;
            for (var index = 1; index < rows.Count; index++)
            {
                var cells = rows[index].Cells;
                if (cells.Length < headerColumnCount)
                    rows[index] = rows[index] with
                    {
                        Cells = [.. cells, .. Enumerable.Repeat(string.Empty, headerColumnCount - cells.Length)]
                    };
            }
        }

        return rows;
    }

    private static string CellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
            return string.Concat(cell.Descendants(SheetNamespace + "t").Select(text => text.Value));

        var value = cell.Element(SheetNamespace + "v")?.Value ?? string.Empty;
        if (type == "s")
        {
            if (!int.TryParse(value, out var sharedStringIndex)
                || sharedStringIndex < 0
                || sharedStringIndex >= sharedStrings.Count)
                throw InvalidFile("O XLSX contém uma referência de texto inválida.");
            return sharedStrings[sharedStringIndex];
        }

        return type == "b" ? value == "1" ? "TRUE" : "FALSE" : value;
    }

    private static int ColumnNumber(string reference)
    {
        var column = 0;
        foreach (var character in reference)
        {
            if (!char.IsAsciiLetter(character))
                break;
            column = checked(column * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }
        if (column == 0)
            throw InvalidFile("O XLSX contém uma célula sem coluna válida.");
        return column;
    }

    private static string ResolveWorksheetTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw InvalidFile("O XLSX contém um destino de planilha inválido.");
        var normalized = target.Replace('\\', '/').TrimStart('/');
        if (!normalized.StartsWith("xl/", StringComparison.Ordinal))
            normalized = "xl/" + normalized;
        if (normalized.Split('/').Any(segment => segment is ".." or "."))
            throw InvalidFile("O XLSX contém um destino de planilha inválido.");
        return normalized;
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
        => TryLoadXml(archive, path) ?? throw InvalidFile("O XLSX está incompleto.");

    private static XDocument? TryLoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry is null)
            return null;
        if (entry.Length > MaxXmlEntryBytes)
            throw InvalidFile("O XLSX contém uma estrutura acima do limite permitido.");

        using var entryStream = entry.Open();
        using var reader = XmlReader.Create(entryStream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxXmlCharacters,
            MaxCharactersFromEntities = 0
        });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static PatientImportParseException InvalidFile(string message)
        => new(ErrorCodes.InvalidFormat, message);

    private static PatientImportParseException TooManyRows(int maxRows)
        => new(ErrorCodes.TooManyRows, $"O arquivo excede o limite de {maxRows} linhas.");

    private sealed record CsvRecord(int Line, string[] Cells);
}

internal sealed class PatientImportParseException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
