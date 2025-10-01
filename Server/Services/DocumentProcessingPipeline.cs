using Microsoft.Extensions.Logging;
using SmartCollectAPI.Models;
using SmartCollectAPI.Services.Providers;
using SmartCollectAPI.Services.Pipeline;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pgvector;

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
    private readonly ITextChunkingService _chunkingService;
    private readonly IDecisionEngine _decisionEngine;
    private readonly IEmbeddingProviderFactory _embeddingProviderFactory;

    public DocumentProcessingPipeline(
        ILogger<DocumentProcessingPipeline> logger,
        IProviderFactory providerFactory,
        IStorageService storageService,
        IContentDetector contentDetector,
        IJsonParser jsonParser,
        IXmlParser xmlParser,
        ICsvParser csvParser,
        ITextChunkingService chunkingService,
        IDecisionEngine decisionEngine,
        IEmbeddingProviderFactory embeddingProviderFactory)
    {
        _logger = logger;
        _providerFactory = providerFactory;
        _storageService = storageService;
        _contentDetector = contentDetector;
        _jsonParser = jsonParser;
        _xmlParser = xmlParser;
        _csvParser = csvParser;
        _chunkingService = chunkingService;
        _decisionEngine = decisionEngine;
        _embeddingProviderFactory = embeddingProviderFactory;
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
            var fileInfo = new FileInfo(absPath);
            
            // Step 2: Detect content type if needed
            var detectedMimeType = job.MimeType;
            if (string.IsNullOrWhiteSpace(detectedMimeType) || detectedMimeType == "application/octet-stream")
            {
                detectedMimeType = await _contentDetector.DetectMimeAsync(fileStream, job.MimeType, cancellationToken);
                fileStream.Position = 0; // Reset position after detection
            }

            _logger.LogInformation("Processing document with MIME type: {MimeType}", detectedMimeType);

            // Step 2.5: Generate processing plan using Decision Engine
            // Read a preview of the content for the decision engine
            string? contentPreview = null;
            try
            {
                if (fileStream.CanSeek)
                {
                    using var previewReader = new StreamReader(fileStream, leaveOpen: true);
                    var previewBuffer = new char[500];
                    var charsRead = await previewReader.ReadAsync(previewBuffer, 0, 500);
                    contentPreview = new string(previewBuffer, 0, charsRead);
                    fileStream.Position = 0; // Reset after preview
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read content preview, continuing without it");
            }

            var processingPlan = await _decisionEngine.GeneratePlanAsync(
                fileName: fileInfo.Name,
                fileSize: fileInfo.Length,
                mimeType: detectedMimeType,
                contentPreview: contentPreview,
                metadata: null);

            _logger.LogInformation("Generated processing plan for {FileName}: Provider={Provider}, Strategy={Strategy}, ChunkSize={ChunkSize}, RequiresOCR={RequiresOCR}, Language={Language}, Priority={Priority}, EstimatedCost={Cost}",
                fileInfo.Name,
                processingPlan.EmbeddingProvider,
                processingPlan.ChunkingStrategy,
                processingPlan.ChunkSize,
                processingPlan.RequiresOCR,
                processingPlan.Language,
                processingPlan.Priority,
                processingPlan.EstimatedCost);

            // Log decision reasons for audit trail
            if (processingPlan.DecisionReasons != null && processingPlan.DecisionReasons.Any())
            {
                _logger.LogInformation("Decision reasons: {Reasons}", string.Join("; ", processingPlan.DecisionReasons));
            }

            // Step 3: Parse document based on type
            var parseResult = await ParseDocumentAsync(fileStream, detectedMimeType, cancellationToken);
            // Note: Stream may be closed by parser, so don't try to reset position

            var extractedText = SanitizeText(parseResult.ExtractedText);

            // Step 4: Extract entities from text
            EntityExtractionResult? entityResult = null;
            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                var entityService = _providerFactory.GetEntityExtractionService();
                entityResult = await entityService.ExtractEntitiesAsync(extractedText, cancellationToken);
            }

            // Step 5: Chunk text for better semantic search (using plan parameters)
            List<TextChunk>? chunks = null;
            if (!string.IsNullOrWhiteSpace(extractedText) && extractedText.Length > 2000)
            {
                _logger.LogInformation("Chunking text ({Length} chars) using strategy: {Strategy}, size: {Size}, overlap: {Overlap}", 
                    extractedText.Length, processingPlan.ChunkingStrategy, processingPlan.ChunkSize, processingPlan.ChunkOverlap);
                
                // Parse the chunking strategy from the plan
                var strategy = Enum.TryParse<ChunkingStrategy>(processingPlan.ChunkingStrategy, ignoreCase: true, out var parsedStrategy)
                    ? parsedStrategy
                    : ChunkingStrategy.SlidingWindow;
                
                var chunkingOptions = new ChunkingOptions(
                    MaxTokens: processingPlan.ChunkSize,
                    OverlapTokens: processingPlan.ChunkOverlap,
                    Strategy: strategy
                );
                
                chunks = _chunkingService.ChunkText(extractedText, chunkingOptions);
                _logger.LogInformation("Created {ChunkCount} chunks from document", chunks.Count);
            }

            // Step 6: Generate embeddings for chunks or full document (using provider from plan)
            IEmbeddingService embeddingService;
            try
            {
                embeddingService = _embeddingProviderFactory.GetProvider(processingPlan.EmbeddingProvider);
                _logger.LogInformation("Using embedding provider: {Provider} (from plan)", processingPlan.EmbeddingProvider);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get embedding provider {Provider}, falling back to default", 
                    processingPlan.EmbeddingProvider);
                embeddingService = _embeddingProviderFactory.GetDefaultProvider();
            }

            EmbeddingResult embeddingResult;
            List<ChunkEmbedding>? chunkEmbeddings = null;
            
            if (chunks != null && chunks.Any())
            {
                // Generate embeddings for each chunk
                chunkEmbeddings = new List<ChunkEmbedding>();
                
                foreach (var chunk in chunks)
                {
                    var chunkEmbedding = await embeddingService.GenerateEmbeddingAsync(chunk.Content, cancellationToken);
                    
                    if (chunkEmbedding.Success)
                    {
                        chunkEmbeddings.Add(new ChunkEmbedding(
                            ChunkIndex: chunk.ChunkIndex,
                            Content: chunk.Content,
                            StartOffset: chunk.StartOffset,
                            EndOffset: chunk.EndOffset,
                            Embedding: chunkEmbedding.Embedding,
                            Metadata: chunk.Metadata
                        ));
                    }
                }
                
                _logger.LogInformation("Generated embeddings for {Count} chunks using {Provider}", 
                    chunkEmbeddings.Count, processingPlan.EmbeddingProvider);
                
                // Compute mean-of-chunks as document-level embedding
                if (chunkEmbeddings.Count > 0 && chunkEmbeddings[0].Embedding != null)
                {
                    var meanEmbedding = ComputeMeanEmbedding(chunkEmbeddings);
                    embeddingResult = new EmbeddingResult(meanEmbedding);
                    _logger.LogInformation("Computed mean-of-chunks embedding ({Dimensions} dims) from {Count} chunks",
                        meanEmbedding.ToArray().Length, chunkEmbeddings.Count);
                }
                else
                {
                    var emptyVector = new Vector(new float[embeddingService.EmbeddingDimensions]);
                    embeddingResult = new EmbeddingResult(emptyVector, false, "No chunk embeddings generated");
                }
            }
            else
            {
                // Generate single embedding for the whole document
                var textToEmbed = PrepareTextForEmbedding(extractedText, parseResult.Metadata);
                embeddingResult = await embeddingService.GenerateEmbeddingAsync(textToEmbed, cancellationToken);
            }

            // Step 7: Create canonical document
            var canonicalDoc = CreateCanonicalDocument(job, parseResult, entityResult, embeddingResult);

            // Step 8: Send notification if requested
            NotificationResult? notificationResult = null;
            if (!string.IsNullOrWhiteSpace(job.NotifyEmail))
            {
                notificationResult = await SendNotificationAsync(job, canonicalDoc, cancellationToken);
            }

            _logger.LogInformation("Successfully processed document for job {JobId}", job.JobId);

            return new PipelineResult(
                Success: true,
                CanonicalDocument: canonicalDoc,
                ChunkEmbeddings: chunkEmbeddings,
                NotificationResult: notificationResult,
                EmbeddingProvider: processingPlan.EmbeddingProvider,
                EmbeddingDimensions: embeddingService.EmbeddingDimensions
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
                    ["structured_data"] = structuredData ?? new JsonObject(),
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
            Id = job.JobId,
            SourceUri = job.SourceUri,
            IngestTs = job.ReceivedAt,
            Mime = job.MimeType,
            Structured = isStructured,
            StructuredPayload = structuredPayload,
            ExtractedText = SanitizeText(parseResult.ExtractedText),
            Entities = entities,
            Tables = tables,
            Sections = sections,
            Embedding = embeddingResult.Success ? embeddingResult.Embedding?.ToArray() : null,
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
            
            <p><em>Processed by SmartCollectAPI using open source services</em></p>
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

    private static string SanitizeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var sanitized = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')
            {
                sanitized.Append(' ');
            }
            else
            {
                sanitized.Append(ch);
            }
        }

        return sanitized.ToString();
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

    /// <summary>
    /// Compute the mean embedding from a list of chunk embeddings.
    /// This creates a document-level embedding that represents the average semantic meaning.
    /// </summary>
    private static Vector ComputeMeanEmbedding(List<ChunkEmbedding> chunkEmbeddings)
    {
        if (chunkEmbeddings == null || chunkEmbeddings.Count == 0)
        {
            throw new ArgumentException("Cannot compute mean of empty chunk list", nameof(chunkEmbeddings));
        }

        // Get dimensions from first chunk
        var firstEmbedding = chunkEmbeddings[0].Embedding;
        if (firstEmbedding == null)
        {
            throw new ArgumentException("First chunk has no embedding", nameof(chunkEmbeddings));
        }

        var dims = firstEmbedding.ToArray().Length;
        var sum = new float[dims];

        // Sum all embeddings
        int validCount = 0;
        foreach (var chunk in chunkEmbeddings)
        {
            if (chunk.Embedding != null)
            {
                var embedding = chunk.Embedding.ToArray();
                if (embedding.Length == dims)
                {
                    for (int i = 0; i < dims; i++)
                    {
                        sum[i] += embedding[i];
                    }
                    validCount++;
                }
            }
        }

        // Compute average
        if (validCount > 0)
        {
            for (int i = 0; i < dims; i++)
            {
                sum[i] /= validCount;
            }
        }

        return new Vector(sum);
    }
}

public record PipelineResult(
    bool Success,
    CanonicalDocument? CanonicalDocument,
    List<ChunkEmbedding>? ChunkEmbeddings = null,
    NotificationResult? NotificationResult = null,
    string? ErrorMessage = null,
    string? EmbeddingProvider = null,
    int? EmbeddingDimensions = null
);

public record ChunkEmbedding(
    int ChunkIndex,
    string Content,
    int StartOffset,
    int EndOffset,
    Vector? Embedding,
    Dictionary<string, object> Metadata
);
