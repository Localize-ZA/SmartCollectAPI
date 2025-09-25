using Google.Cloud.DocumentAI.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Google.Protobuf;

namespace SmartCollectAPI.Services.Providers;

public class GoogleDocAiParser : IAdvancedDocumentParser
{
    private readonly ILogger<GoogleDocAiParser> _logger;
    private readonly DocumentProcessorServiceClient _client;
    private readonly GoogleCloudOptions _options;

    private readonly HashSet<string> _supportedMimeTypes = new()
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    public GoogleDocAiParser(ILogger<GoogleDocAiParser> logger, IOptions<GoogleCloudOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        try
        {
            _client = DocumentProcessorServiceClient.Create();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Document AI client");
            throw;
        }
    }

    public bool CanHandle(string mimeType)
    {
        return _supportedMimeTypes.Contains(mimeType.ToLowerInvariant());
    }

    public async Task<DocumentParseResult> ParseAsync(Stream documentStream, string mimeType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing document with Google Document AI, MIME type: {MimeType}", mimeType);

            // Read the document content
            using var memoryStream = new MemoryStream();
            await documentStream.CopyToAsync(memoryStream, cancellationToken);
            var documentContent = memoryStream.ToArray();

            // Create the raw document
            var rawDocument = new RawDocument
            {
                Content = ByteString.CopyFrom(documentContent),
                MimeType = mimeType
            };

            // Create the process request
            if (string.IsNullOrWhiteSpace(_options.ProcessorId))
            {
                throw new InvalidOperationException("Google Document AI ProcessorId is not configured. Please set a valid ProcessorId in GoogleCloudOptions.");
            }
            var processorName = ProcessorName.FromProjectLocationProcessor(
                _options.ProjectId, 
                _options.Location ?? "us", 
                _options.ProcessorId);

            var request = new ProcessRequest
            {
                Name = processorName.ToString(),
                RawDocument = rawDocument
            };

            // Process the document
            var response = await _client.ProcessDocumentAsync(request, cancellationToken);
            var document = response.Document;

            // Extract text
            var extractedText = document.Text ?? string.Empty;

            // Extract entities
            var entities = document.Entities?.Select(entity => new ExtractedEntity(
                Name: entity.Type,
                Type: entity.Type,
                Salience: (double)entity.Confidence,
                Mentions: entity.TextAnchor?.TextSegments?.Select(segment => new EntityMention(
                    Text: extractedText.Substring((int)segment.StartIndex, (int)(segment.EndIndex - segment.StartIndex)),
                    StartOffset: (int)segment.StartIndex,
                    EndOffset: (int)segment.EndIndex
                )).ToList()
            )).ToList() ?? new List<ExtractedEntity>();

            // Extract tables
            var tables = document.Pages?.SelectMany(page => page.Tables ?? Enumerable.Empty<Document.Types.Page.Types.Table>())
                .Select(table => ExtractTable(table, extractedText))
                .ToList() ?? new List<ExtractedTable>();

            // Extract sections/pages
            var sections = document.Pages?.Select((page, index) => new DocumentSection(
                Title: $"Page {index + 1}",
                Content: ExtractPageText(page, extractedText),
                PageNumber: index + 1,
                Metadata: new Dictionary<string, object>
                {
                    ["width"] = page.Dimension?.Width ?? 0,
                    ["height"] = page.Dimension?.Height ?? 0,
                    ["unit"] = page.Dimension?.Unit ?? ""
                }
            )).ToList() ?? new List<DocumentSection>();

            var metadata = new Dictionary<string, object>
            {
                ["pageCount"] = document.Pages?.Count ?? 0,
                ["language"] = document.Pages?.FirstOrDefault()?.DetectedLanguages?.FirstOrDefault()?.LanguageCode ?? "unknown"
            };

            _logger.LogInformation("Successfully processed document with Google Document AI. Pages: {PageCount}, Entities: {EntityCount}", 
                metadata["pageCount"], entities.Count);

            return new DocumentParseResult(
                ExtractedText: extractedText,
                Entities: entities,
                Tables: tables,
                Sections: sections,
                Metadata: metadata,
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document with Google Document AI");
            return new DocumentParseResult(
                ExtractedText: string.Empty,
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    private ExtractedTable ExtractTable(Document.Types.Page.Types.Table table, string documentText)
    {
        var rows = new List<List<string>>();
        
        if (table.HeaderRows != null)
        {
            foreach (var headerRow in table.HeaderRows)
            {
                var cellTexts = ExtractRowText(headerRow, documentText);
                rows.Add(cellTexts);
            }
        }

        if (table.BodyRows != null)
        {
            foreach (var bodyRow in table.BodyRows)
            {
                var cellTexts = ExtractRowText(bodyRow, documentText);
                rows.Add(cellTexts);
            }
        }

        return new ExtractedTable(
            RowCount: rows.Count,
            ColumnCount: rows.FirstOrDefault()?.Count ?? 0,
            Rows: rows,
            Metadata: new Dictionary<string, object>
            {
                ["hasHeaderRows"] = table.HeaderRows?.Count > 0,
                ["bodyRowCount"] = table.BodyRows?.Count ?? 0
            }
        );
    }

    private List<string> ExtractRowText(Document.Types.Page.Types.Table.Types.TableRow row, string documentText)
    {
        return row.Cells?.Select(cell => ExtractCellText(cell, documentText)).ToList() ?? new List<string>();
    }

    private string ExtractCellText(Document.Types.Page.Types.Table.Types.TableCell cell, string documentText)
    {
        if (cell.Layout?.TextAnchor?.TextSegments == null) return string.Empty;
        
        var texts = cell.Layout.TextAnchor.TextSegments
            .Select(segment => documentText.Substring((int)segment.StartIndex, (int)(segment.EndIndex - segment.StartIndex)))
            .ToList();
        
        return string.Join(" ", texts).Trim();
    }

    private string ExtractPageText(Document.Types.Page page, string documentText)
    {
        if (page.Layout?.TextAnchor?.TextSegments == null) return string.Empty;
        
        var texts = page.Layout.TextAnchor.TextSegments
            .Select(segment => documentText.Substring((int)segment.StartIndex, (int)(segment.EndIndex - segment.StartIndex)))
            .ToList();
        
        return string.Join(" ", texts).Trim();
    }
}

public class GoogleCloudOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ProcessorId { get; set; }
    public string? CredentialsPath { get; set; }
}