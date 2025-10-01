using Microsoft.Extensions.Logging;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartCollectAPI.Services.Providers;

/// <summary>
/// Advanced PDF parser using iText7 for text extraction and layout analysis
/// Supports text extraction, table detection, and document structure analysis
/// </summary>
public partial class PdfPigParser(ILogger<PdfPigParser> logger) : IAdvancedDocumentParser
{
    private readonly ILogger<PdfPigParser> _logger = logger;

    private readonly HashSet<string> _supportedMimeTypes =
    [
        "application/pdf"
    ];

    public bool CanHandle(string mimeType)
    {
        return _supportedMimeTypes.Contains(mimeType.ToLowerInvariant());
    }

    public async Task<DocumentParseResult> ParseAsync(Stream documentStream, string mimeType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing PDF with iText7 parser (open-source implementation)");

            // Read the PDF stream into a byte array
            using var memoryStream = new MemoryStream();
            await documentStream.CopyToAsync(memoryStream, cancellationToken);
            var pdfBytes = memoryStream.ToArray();

            var extractedText = new StringBuilder();
            var sections = new List<DocumentSection>();
            var tables = new List<ExtractedTable>();
            var entities = new List<ExtractedEntity>();
            var metadata = new Dictionary<string, object>();

            // Process PDF with iText7
            using var reader = new PdfReader(new MemoryStream(pdfBytes));
            using var document = new PdfDocument(reader);

            int numberOfPages = document.GetNumberOfPages();
            metadata["pageCount"] = numberOfPages;
            metadata["parser"] = "iText7";

            // Extract document info
            var info = document.GetDocumentInfo();
            metadata["title"] = info.GetTitle() ?? "";
            metadata["author"] = info.GetAuthor() ?? "";
            metadata["subject"] = info.GetSubject() ?? "";
            metadata["creator"] = info.GetCreator() ?? "";

            // Process each page
            for (int pageNumber = 1; pageNumber <= numberOfPages; pageNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = document.GetPage(pageNumber);

                // Extract text using iText7 text extraction
                var pageText = ExtractPageText(page);
                extractedText.AppendLine(pageText);

                // Create section for this page
                var pageRect = page.GetPageSize();
                sections.Add(new DocumentSection(
                    Title: $"Page {pageNumber}",
                    Content: pageText,
                    PageNumber: pageNumber,
                    Metadata: new Dictionary<string, object>
                    {
                        ["width"] = pageRect.GetWidth(),
                        ["height"] = pageRect.GetHeight(),
                        ["rotation"] = page.GetRotation(),
                        ["textLength"] = pageText.Length
                    }
                ));

                // Extract potential tables (basic table detection)
                var pageTables = ExtractTablesFromPage(page, pageNumber);
                tables.AddRange(pageTables);

                // Extract potential entities (basic pattern matching)
                var pageEntities = ExtractEntitiesFromText(pageText, pageNumber);
                entities.AddRange(pageEntities);
            }

            var finalText = extractedText.ToString().Trim();
            metadata["extractedTextLength"] = finalText.Length;
            metadata["tablesFound"] = tables.Count;
            metadata["entitiesFound"] = entities.Count;

            _logger.LogInformation("Successfully processed PDF with iText7. Pages: {PageCount}, Text Length: {TextLength}, Tables: {TableCount}",
                metadata["pageCount"], metadata["extractedTextLength"], metadata["tablesFound"]);

            return new DocumentParseResult(
                ExtractedText: finalText,
                Entities: entities,
                Tables: tables,
                Sections: sections,
                Metadata: metadata,
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PDF with iText7 parser");
            return new DocumentParseResult(
                ExtractedText: string.Empty,
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    private string ExtractPageText(PdfPage page)
    {
        try
        {
            // Use iText7's text extraction with location strategy for better text ordering
            var strategy = new LocationTextExtractionStrategy();
            var text = PdfTextExtractor.GetTextFromPage(page, strategy);
            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting text with location strategy, falling back to simple extraction");
            // Fallback to simple text extraction
            return PdfTextExtractor.GetTextFromPage(page).Trim();
        }
    }

    private List<ExtractedTable> ExtractTablesFromPage(PdfPage page, int pageNumber)
    {
        var tables = new List<ExtractedTable>();

        try
        {
            // Basic table detection using text analysis
            var text = ExtractPageText(page);
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Look for patterns that suggest tables (multiple columns, consistent spacing)
            var potentialTables = IdentifyTableStructures(lines);

            foreach (var tableData in potentialTables)
            {
                var table = new ExtractedTable(
                    RowCount: tableData.Rows.Count,
                    ColumnCount: tableData.Headers.Count,
                    Rows: tableData.Rows,
                    Metadata: new Dictionary<string, object>
                    {
                        ["pageNumber"] = pageNumber,
                        ["detectionMethod"] = "text-pattern-analysis",
                        ["headers"] = tableData.Headers,
                        ["confidence"] = tableData.Confidence
                    }
                );
                tables.Add(table);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting tables on page {PageNumber}", pageNumber);
        }

        return tables;
    }

    private List<ExtractedEntity> ExtractEntitiesFromText(string text, int pageNumber)
    {
        var entities = new List<ExtractedEntity>();

        try
        {
            // Email addresses
            var emailMatches = MyRegex().Matches(text);
            foreach (Match match in emailMatches)
            {
                entities.Add(new ExtractedEntity(
                    Name: match.Value,
                    Type: "Email",
                    Salience: 0.9,
                    Mentions:
                    [
                        new(match.Value, match.Index, match.Index + match.Length)
                    ]
                ));
            }

            // Phone numbers
            var phoneMatches = Regex.Matches(text, @"\b(?:\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})\b");
            foreach (Match match in phoneMatches)
            {
                entities.Add(new ExtractedEntity(
                    Name: match.Value,
                    Type: "Phone",
                    Salience: 0.8,
                    Mentions:
                    [
                        new(match.Value, match.Index, match.Index + match.Length)
                    ]
                ));
            }

            // Dates (various formats)
            var dateMatches = Regex.Matches(text, @"\b(?:(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2},?\s+\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}-\d{2}-\d{2})\b");
            foreach (Match match in dateMatches)
            {
                entities.Add(new ExtractedEntity(
                    Name: match.Value,
                    Type: "Date",
                    Salience: 0.7,
                    Mentions:
                    [
                        new(match.Value, match.Index, match.Index + match.Length)
                    ]
                ));
            }

            // Currency amounts
            var currencyMatches = Regex.Matches(text, @"\$[\d,]+\.?\d*|\b\d+\.\d{2}\s*(?:USD|EUR|GBP|CAD)\b");
            foreach (Match match in currencyMatches)
            {
                entities.Add(new ExtractedEntity(
                    Name: match.Value,
                    Type: "Currency",
                    Salience: 0.8,
                    Mentions:
                    [
                        new(match.Value, match.Index, match.Index + match.Length)
                    ]
                ));
            }

            // URLs
            var urlMatches = Regex.Matches(text, @"https?://[^\s]+");
            foreach (Match match in urlMatches)
            {
                entities.Add(new ExtractedEntity(
                    Name: match.Value,
                    Type: "URL",
                    Salience: 0.9,
                    Mentions:
                    [
                        new(match.Value, match.Index, match.Index + match.Length)
                    ]
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting entities from page {PageNumber}", pageNumber);
        }

        return entities;
    }

    private List<TableData> IdentifyTableStructures(string[] lines)
    {
        var tables = new List<TableData>();
        var currentTable = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentTable.Count > 2) // At least header + 1 row
                {
                    var tableData = ParseTableFromLines(currentTable);
                    if (tableData != null)
                        tables.Add(tableData);
                }
                currentTable.Clear();
                continue;
            }

            // Check if line looks like a table row (multiple columns separated by spaces/tabs)
            if (IsLikelyTableRow(line))
            {
                currentTable.Add(line);
            }
            else
            {
                // End of potential table
                if (currentTable.Count > 2)
                {
                    var tableData = ParseTableFromLines(currentTable);
                    if (tableData != null)
                        tables.Add(tableData);
                }
                currentTable.Clear();
            }
        }

        // Check final table
        if (currentTable.Count > 2)
        {
            var tableData = ParseTableFromLines(currentTable);
            if (tableData != null)
                tables.Add(tableData);
        }

        return tables;
    }

    private static bool IsLikelyTableRow(string line)
    {
        // Check for patterns that suggest table structure
        var tabCount = line.Count(c => c == '\t');
        var spaceGroups = Regex.Matches(line, @"\s{2,}").Count;

        // If line has multiple tabs or space groups, it might be a table row
        return tabCount >= 2 || spaceGroups >= 2;
    }

    private TableData? ParseTableFromLines(List<string> lines)
    {
        if (lines.Count < 2) return null;

        try
        {
            var headers = new List<string>();
            var rows = new List<List<string>>();

            // Parse first line as headers
            var headerLine = lines[0];
            if (headerLine.Contains('\t'))
            {
                headers = [.. headerLine.Split('\t', StringSplitOptions.RemoveEmptyEntries).Select(h => h.Trim())];
            }
            else
            {
                // Split by multiple spaces
                headers = [.. Regex.Split(headerLine, @"\s{2,}")
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Select(h => h.Trim())];
            }

            if (headers.Count < 2) return null;

            // Parse remaining lines as data rows
            for (int i = 1; i < lines.Count; i++)
            {
                var rowLine = lines[i];
                List<string> rowData;

                if (rowLine.Contains('\t'))
                {
                    rowData = [.. rowLine.Split('\t', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim())];
                }
                else
                {
                    rowData = [.. Regex.Split(rowLine, @"\s{2,}")
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c.Trim())];
                }

                // Pad or trim to match header count
                while (rowData.Count < headers.Count)
                    rowData.Add("");

                if (rowData.Count > headers.Count)
                    rowData = [.. rowData.Take(headers.Count)];

                rows.Add(rowData);
            }

            // Calculate confidence based on consistency
            var confidence = CalculateTableConfidence(headers, rows);

            if (confidence >= 0.5) // Only return tables with reasonable confidence
            {
                return new TableData
                {
                    Headers = headers,
                    Rows = rows,
                    Confidence = confidence
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing table structure");
        }

        return null;
    }

    private static double CalculateTableConfidence(List<string> headers, List<List<string>> rows)
    {
        if (headers.Count < 2 || rows.Count < 1) return 0.0;

        var score = 0.0;

        // Points for having headers
        score += 0.3;

        // Points for consistent column count
        var consistentColumns = rows.All(r => r.Count == headers.Count);
        if (consistentColumns) score += 0.3;

        // Points for having data in most cells
        var totalCells = rows.Sum(r => r.Count);
        var filledCells = rows.Sum(r => r.Count(c => !string.IsNullOrWhiteSpace(c)));
        var fillRatio = totalCells > 0 ? (double)filledCells / totalCells : 0;
        score += fillRatio * 0.4;

        return Math.Min(1.0, score);
    }

    private class TableData
    {
        public List<string> Headers { get; set; } = [];
        public List<List<string>> Rows { get; set; } = [];
        public double Confidence { get; set; }
    }

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b")]
    private static partial Regex MyRegex();
}