using Microsoft.Extensions.Logging;
using SmartCollectAPI.Models;
using SmartCollectAPI.Services.Providers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartCollectAPI.Services;

public interface IDocumentProcessingPipeline
{
    Task<PipelineResult> ProcessDocumentAsync(JobEnvelope job, CancellationToken cancellationToken = default);
}

public class DocumentProcessingPipeline : IDocumentProcessingPipeline
{
    private readonly ILogger<DocumentProcessingPipeline> _logger;
    private readonly IProviderFactory _providerFactory;
    private readonly IStorageService _storageService;
    private readonly IContentDetector _contentDetector;
    private readonly IJsonParser _jsonParser;
    private readonly IXmlParser _xmlParser;
    private readonly ICsvParser _csvParser;

    public DocumentProcessingPipeline(
        ILogger<DocumentProcessingPipeline> logger,
        IProviderFactory providerFactory,
        IStorageService storageService,
        IContentDetector contentDetector,
        IJsonParser jsonParser,
        IXmlParser xmlParser,
        ICsvParser csvParser)
    {
        _logger = logger;
        _providerFactory = providerFactory;
        _storageService = storageService;
        _contentDetector = contentDetector;
        _jsonParser = jsonParser;
        _xmlParser = xmlParser;
        _csvParser = csvParser;
    }

    public async Task<PipelineResult> ProcessDocumentAsync(JobEnvelope job, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting document processing pipeline for job {JobId}", job.JobId);

            // Step 1: Load the document - for now, we'll use direct file access since IStorageService doesn't have Load method
            var localPath = job.SourceUri;
            var absPath = Path.IsPathRooted(localPath) ? localPath : Path.Combine(AppContext.BaseDirectory, localPath);
            
            if (!File.Exists(absPath))
            {
                throw new FileNotFoundException($"Source file not found: {absPath}");
            }

            await using var fileStream = File.OpenRead(absPath);
            
            // Step 2: Detect content type if needed
            var detectedMimeType = job.MimeType;
            if (string.IsNullOrWhiteSpace(detectedMimeType) || detectedMimeType == "application/octet-stream")
            {
                detectedMimeType = await _contentDetector.DetectMimeAsync(fileStream, job.MimeType, cancellationToken);
                fileStream.Position = 0; // Reset position after detection
            }

            _logger.LogInformation("Processing document with MIME type: {MimeType}", detectedMimeType);

            // Step 3: Parse document based on type
            var parseResult = await ParseDocumentAsync(fileStream, detectedMimeType, cancellationToken);
            fileStream.Position = 0; // Reset for potential reuse

            // Step 4: Extract entities from text
            EntityExtractionResult? entityResult = null;
            if (!string.IsNullOrWhiteSpace(parseResult.ExtractedText))
            {
                var entityService = _providerFactory.GetEntityExtractionService();
                entityResult = await entityService.ExtractEntitiesAsync(parseResult.ExtractedText, cancellationToken);
            }

            // Step 5: Generate embeddings
            var embeddingService = _providerFactory.GetEmbeddingService();
            var textToEmbed = PrepareTextForEmbedding(parseResult.ExtractedText, parseResult.Metadata);
            var embeddingResult = await embeddingService.GenerateEmbeddingAsync(textToEmbed, cancellationToken);

            // Step 6: Create canonical document
            var canonicalDoc = CreateCanonicalDocument(job, parseResult, entityResult, embeddingResult);

            // Step 7: Send notification if requested
            NotificationResult? notificationResult = null;
            if (!string.IsNullOrWhiteSpace(job.NotifyEmail))
            {
                notificationResult = await SendNotificationAsync(job, canonicalDoc, cancellationToken);
            }

            _logger.LogInformation("Successfully processed document for job {JobId}", job.JobId);

            return new PipelineResult(
                Success: true,
                CanonicalDocument: canonicalDoc,
                NotificationResult: notificationResult
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document for job {JobId}", job.JobId);
            return new PipelineResult(
                Success: false,
                ErrorMessage: ex.Message,
                CanonicalDocument: null
            );
        }
    }

    private async Task<DocumentParseResult> ParseDocumentAsync(Stream documentStream, string mimeType, CancellationToken cancellationToken)
    {
        // First try structured data parsers (JSON, XML, CSV)
        if (IsStructuredData(mimeType))
        {
            return await ParseStructuredDataAsync(documentStream, mimeType, cancellationToken);
        }

        // Try image OCR
        if (IsImage(mimeType))
        {
            var ocrService = _providerFactory.GetOcrService();
            if (ocrService.CanHandle(mimeType))
            {
                var ocrResult = await ocrService.ExtractTextAsync(documentStream, mimeType, cancellationToken);
                return new DocumentParseResult(
                    ExtractedText: ocrResult.ExtractedText,
                    Success: ocrResult.Success,
                    ErrorMessage: ocrResult.ErrorMessage,
                    Metadata: new Dictionary<string, object>
                    {
                        ["parser"] = "OCR",
                        ["annotations"] = ocrResult.Annotations?.Count ?? 0,
                        ["objects"] = ocrResult.Objects?.Count ?? 0
                    }
                );
            }
        }

        // Try advanced document parsing (PDF, Word, etc.)
        var documentParser = _providerFactory.GetDocumentParser();
        if (documentParser.CanHandle(mimeType))
        {
            return await documentParser.ParseAsync(documentStream, mimeType, cancellationToken);
        }

        // Fallback: treat as plain text
        documentStream.Position = 0;
        using var reader = new StreamReader(documentStream);
        var text = await reader.ReadToEndAsync(cancellationToken);
        
        return new DocumentParseResult(
            ExtractedText: text,
            Success: true,
            Metadata: new Dictionary<string, object> { ["parser"] = "PlainText" }
        );
    }

    private async Task<DocumentParseResult> ParseStructuredDataAsync(Stream documentStream, string mimeType, CancellationToken cancellationToken)
    {
        try
        {
            JsonNode? structuredData = null;
            string extractedText = string.Empty;

            switch (mimeType.ToLowerInvariant())
            {
                case "application/json":
                case "text/json":
                    structuredData = await _jsonParser.ParseAsync(documentStream, cancellationToken);
                    extractedText = structuredData?.ToJsonString() ?? string.Empty;
                    break;

                case "application/xml":
                case "text/xml":
                    structuredData = await _xmlParser.ParseAsync(documentStream, cancellationToken);
                    extractedText = structuredData?.ToJsonString() ?? string.Empty;
                    break;

                case "text/csv":
                case "application/csv":
                    structuredData = await _csvParser.ParseAsync(documentStream, cancellationToken);
                    extractedText = structuredData?.ToJsonString() ?? string.Empty;
                    break;
            }

            return new DocumentParseResult(
                ExtractedText: extractedText,
                Success: true,
                Metadata: new Dictionary<string, object>
                {
                    ["parser"] = "Structured",
                    ["structured_data"] = structuredData,
                    ["is_structured"] = true
                }
            );
        }
        catch (Exception ex)
        {
            return new DocumentParseResult(
                ExtractedText: string.Empty,
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    private CanonicalDocument CreateCanonicalDocument(
        JobEnvelope job,
        DocumentParseResult parseResult,
        EntityExtractionResult? entityResult,
        EmbeddingResult embeddingResult)
    {
        var entities = new JsonArray();
        if (entityResult?.Entities != null)
        {
            foreach (var entity in entityResult.Entities)
            {
                entities.Add(JsonNode.Parse(JsonSerializer.Serialize(new
                {
                    name = entity.Name,
                    type = entity.Type,
                    salience = entity.Salience,
                    mentions = entity.Mentions?.Select(m => new
                    {
                        text = m.Text,
                        start_offset = m.StartOffset,
                        end_offset = m.EndOffset
                    }).ToArray()
                })));
            }
        }

        var tables = new JsonArray();
        if (parseResult.Tables != null)
        {
            foreach (var table in parseResult.Tables)
            {
                tables.Add(JsonNode.Parse(JsonSerializer.Serialize(table)));
            }
        }

        var sections = new JsonArray();
        if (parseResult.Sections != null)
        {
            foreach (var section in parseResult.Sections)
            {
                sections.Add(JsonNode.Parse(JsonSerializer.Serialize(section)));
            }
        }

        var isStructured = parseResult.Metadata?.ContainsKey("is_structured") == true;
        var structuredPayload = isStructured ? parseResult.Metadata?["structured_data"] as JsonNode : null;

        return new CanonicalDocument
        {
            Id = job.Id,
            SourceUri = job.SourceUri,
            IngestTs = job.ReceivedAt,
            Mime = job.MimeType,
            Structured = isStructured,
            StructuredPayload = structuredPayload,
            ExtractedText = parseResult.ExtractedText,
            Entities = entities,
            Tables = tables,
            Sections = sections,
            EmbeddingDim = embeddingResult.Success ? _providerFactory.GetEmbeddingService().EmbeddingDimensions : 0,
            ProcessingStatus = parseResult.Success && embeddingResult.Success ? "processed" : "failed",
            ProcessingErrors = !parseResult.Success || !embeddingResult.Success ? 
                JsonNode.Parse(JsonSerializer.Serialize(new
                {
                    parse_error = parseResult.ErrorMessage,
                    embedding_error = embeddingResult.ErrorMessage
                })) : null,
            SchemaVersion = "v1"
        };
    }

    private async Task<NotificationResult> SendNotificationAsync(
        JobEnvelope job,
        CanonicalDocument canonicalDoc,
        CancellationToken cancellationToken)
    {
        try
        {
            var notificationService = _providerFactory.GetNotificationService();
            
            var subject = $"Document Processing Complete: {Path.GetFileName(job.SourceUri)}";
            var body = CreateNotificationBody(canonicalDoc);
            
            var attachments = new List<NotificationAttachment>
            {
                new(
                    FileName: "processed_document.json",
                    ContentType: "application/json",
                    Content: System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonicalDoc, new JsonSerializerOptions { WriteIndented = true }))
                )
            };

            var request = new NotificationRequest(
                ToEmail: job.NotifyEmail!,
                Subject: subject,
                Body: body,
                Attachments: attachments,
                IsHtml: true
            );

            return await notificationService.SendNotificationAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification for job {JobId}", job.JobId);
            return new NotificationResult(false, ErrorMessage: ex.Message);
        }
    }

    private static string CreateNotificationBody(CanonicalDocument doc)
    {
        var entityCount = doc.Entities?.AsArray()?.Count ?? 0;
        var tableCount = doc.Tables?.AsArray()?.Count ?? 0;
        var textLength = doc.ExtractedText?.Length ?? 0;

        return $@"
        <html>
        <body>
            <h2>Document Processing Complete</h2>
            <p>Your document has been successfully processed by SmartCollectAPI.</p>
            
            <h3>Processing Summary</h3>
            <ul>
                <li><strong>Status:</strong> {doc.ProcessingStatus}</li>
                <li><strong>Document Type:</strong> {(doc.Structured ? "Structured" : "Unstructured")}</li>
                <li><strong>Text Length:</strong> {textLength:N0} characters</li>
                <li><strong>Entities Extracted:</strong> {entityCount}</li>
                <li><strong>Tables Found:</strong> {tableCount}</li>
                <li><strong>Embedding Dimensions:</strong> {doc.EmbeddingDim}</li>
            </ul>

            {(entityCount > 0 ? $@"
            <h3>Top Entities</h3>
            <ul>
                {string.Join("", doc.Entities?.AsArray()?.Take(5).Select(e => 
                    $"<li><strong>{e?["name"]?.ToString()}</strong> ({e?["type"]?.ToString()}) - Salience: {e?["salience"]?.ToString()}</li>") ?? new string[0])}
            </ul>" : "")}

            <p>The complete processing results are attached as JSON.</p>
            
            <p><em>Processed by SmartCollectAPI using Google Cloud services</em></p>
        </body>
        </html>";
    }

    private static bool IsStructuredData(string mimeType)
    {
        var structuredTypes = new[]
        {
            "application/json", "text/json",
            "application/xml", "text/xml",
            "text/csv", "application/csv"
        };
        return structuredTypes.Contains(mimeType.ToLowerInvariant());
    }

    private static bool IsImage(string mimeType)
    {
        return mimeType.ToLowerInvariant().StartsWith("image/");
    }

    private static string PrepareTextForEmbedding(string? extractedText, Dictionary<string, object>? metadata)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return string.Empty;
        }

        // Add metadata context to improve embedding quality
        var textToEmbed = extractedText;
        if (metadata != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in metadata)
            {
                if (kv.Value is string)
                {
                    if (sb.Length > 0)
                        sb.Append(' ');
                    sb.Append($"{kv.Key}: {kv.Value}");
                }
            }
            var context = sb.ToString();
            if (!string.IsNullOrWhiteSpace(context))
            {
                textToEmbed = $"{context}\n\n{extractedText}";
            }
        }

        // Truncate if too long (rough token estimation: 1 token ≈ 4 characters)
        const int maxTokens = 8000; // Leave some buffer
        if (textToEmbed.Length > maxTokens * 4)
        {
            textToEmbed = textToEmbed.Substring(0, maxTokens * 4);
        }

        return textToEmbed;
    }
}

public record PipelineResult(
    bool Success,
    CanonicalDocument? CanonicalDocument,
    NotificationResult? NotificationResult = null,
    string? ErrorMessage = null
);