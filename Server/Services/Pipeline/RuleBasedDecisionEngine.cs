using SmartCollectAPI.Models;
using SmartCollectAPI.Services;
using System.Text.RegularExpressions;

namespace SmartCollectAPI.Services.Pipeline;

/// <summary>
/// Rule-based implementation of IDecisionEngine.
/// Uses heuristics and rules to determine optimal processing strategies.
/// </summary>
public partial class RuleBasedDecisionEngine(ILogger<RuleBasedDecisionEngine> logger, ILanguageDetectionService languageDetectionService) : IDecisionEngine
{
    private readonly ILogger<RuleBasedDecisionEngine> _logger = logger;
    private readonly ILanguageDetectionService _languageDetectionService = languageDetectionService;

    public async Task<PipelinePlan> GeneratePlanAsync(
        string fileName,
        long fileSize,
        string mimeType,
        string? contentPreview = null,
        Dictionary<string, object>? metadata = null)
    {
        var plan = new PipelinePlan();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        _logger.LogInformation(
            "Generating processing plan for {FileName} ({Size} bytes, {MimeType})",
            fileName, fileSize, mimeType);

        // Rule 1: Determine if OCR is needed
        plan.RequiresOCR = DetermineOCRRequirement(mimeType, extension);
        if (plan.RequiresOCR)
        {
            plan.DecisionReasons.Add($"OCR required for {mimeType}");
        }

        // Rule 2: Determine document type
        plan.DocumentType = DetermineDocumentType(fileName, mimeType, extension, contentPreview);
        plan.DecisionReasons.Add($"Document type: {plan.DocumentType}");

        // Rule 3: Choose chunking strategy based on document type
        (plan.ChunkingStrategy, plan.ChunkSize, plan.ChunkOverlap) =
            DetermineChunkingStrategy(plan.DocumentType, fileSize, contentPreview);
        plan.DecisionReasons.Add(
            $"Chunking: {plan.ChunkingStrategy} (size: {plan.ChunkSize}, overlap: {plan.ChunkOverlap})");

        // Rule 4: Choose embedding provider based on document characteristics
        plan.EmbeddingProvider = DetermineEmbeddingProvider(plan.DocumentType, fileSize, metadata);
        plan.DecisionReasons.Add($"Embedding provider: {plan.EmbeddingProvider}");

        // Rule 5: Determine if NER is needed
        plan.RequiresNER = DetermineNERRequirement(plan.DocumentType, fileSize);
        if (plan.RequiresNER)
        {
            plan.DecisionReasons.Add("NER enabled");
        }

        // Rule 6: Determine priority based on file size and type
        plan.Priority = DeterminePriority(fileSize, plan.DocumentType);
        plan.DecisionReasons.Add($"Priority: {plan.Priority}");

        // Rule 7: Estimate processing cost
        plan.EstimatedCost = EstimateProcessingCost(plan);

        // Rule 8: Detect language (use microservice if available; fall back to heuristic)
        plan.Language = await DetectLanguageWithFallbackAsync(contentPreview);
        if (!string.Equals(plan.Language, "en", StringComparison.OrdinalIgnoreCase))
        {
            plan.DecisionReasons.Add($"Language detected: {plan.Language}");
        }

        // Rule 9: Enable reranking for large, important documents
        plan.UseReranking = fileSize > 100_000 &&
                           (plan.Priority == "high" || plan.Priority == "critical");
        if (plan.UseReranking)
        {
            plan.DecisionReasons.Add("Reranking enabled for large/important document");
        }

        _logger.LogInformation(
            "Generated plan: {Strategy} chunking, {Provider} embeddings, {Cost} cost units",
            plan.ChunkingStrategy, plan.EmbeddingProvider, plan.EstimatedCost);

        return await Task.FromResult(plan);
    }

    public async Task<PipelinePlan> GeneratePlanForStagingAsync(StagingDocument document)
    {
        // Extract content preview from normalized content
        string? contentPreview = null;
        if (document.Normalized != null)
        {
            var normalizedText = document.Normalized.ToJsonString();
            contentPreview = normalizedText.Length > 2000
                ? normalizedText[..2000]
                : normalizedText;
        }

        // Extract metadata
        var metadata = new Dictionary<string, object>();
        if (document.RawMetadata != null)
        {
            // Add any relevant metadata from RawMetadata JsonObject
            metadata["mime"] = document.Mime ?? "unknown";
            metadata["source"] = document.SourceUri ?? "unknown";
        }

        return await GeneratePlanAsync(
            document.SourceUri ?? "unknown",
            0, // Size not available in StagingDocument
            document.Mime ?? "application/octet-stream",
            contentPreview,
            metadata);
    }

    private static bool DetermineOCRRequirement(string mimeType, string extension)
    {
        // Images and scanned PDFs need OCR
        if (mimeType.StartsWith("image/")) return true;

        // PDFs might need OCR (we'll determine this later in processing)
        if (mimeType == "application/pdf" || extension == ".pdf") return false; // Let PDF parser decide

        return false;
    }

    private static string DetermineDocumentType(string fileName, string mimeType, string extension, string? contentPreview)
    {
        // Code files
        if (extension is ".cs" or ".js" or ".ts" or ".py" or ".java" or ".cpp" or ".html" or ".css")
            return "code";

        // Markdown and documentation
        if (extension is ".md" or ".markdown" or ".rst")
            return "markdown";

        // Structured documents
        if (extension is ".json" or ".xml" or ".yaml" or ".yml")
            return "structured";

        // Spreadsheets and data
        if (extension is ".csv" or ".xlsx" or ".xls")
            return "tabular";

        // Legal/contracts (heuristic based on content)
        if (contentPreview != null &&
            (contentPreview.Contains("WHEREAS", StringComparison.OrdinalIgnoreCase) ||
             contentPreview.Contains("hereinafter", StringComparison.OrdinalIgnoreCase) ||
             contentPreview.Contains("agreement", StringComparison.OrdinalIgnoreCase)))
            return "legal";

        // Medical documents (heuristic)
        if (contentPreview != null &&
            (contentPreview.Contains("patient", StringComparison.OrdinalIgnoreCase) ||
             contentPreview.Contains("diagnosis", StringComparison.OrdinalIgnoreCase) ||
             contentPreview.Contains("prescription", StringComparison.OrdinalIgnoreCase)))
            return "medical";

        // Technical documentation
        if (contentPreview != null &&
            (contentPreview.Contains("API", StringComparison.OrdinalIgnoreCase) ||
             contentPreview.Contains("documentation", StringComparison.OrdinalIgnoreCase) ||
             contentPreview.Contains("configuration", StringComparison.OrdinalIgnoreCase)))
            return "technical";

        // Default
        return "general";
    }

    private static (string strategy, int size, int overlap) DetermineChunkingStrategy(
        string? documentType, long fileSize, string? contentPreview)
    {
        return documentType switch
        {
            "code" => ("semantic", 800, 100), // Smaller chunks, less overlap for code
            "markdown" => ("markdown", 1500, 200), // Respect markdown structure
            "legal" => ("paragraph", 2000, 300), // Larger chunks for context
            "medical" => ("paragraph", 1500, 250), // Medium chunks with good overlap
            "technical" => ("semantic", 1200, 200), // Semantic chunking for technical docs
            "structured" => ("fixed", 500, 50), // Small chunks for JSON/XML
            "tabular" => ("fixed", 1000, 0), // No overlap for structured data
            _ => ("fixed", 1000, 200) // Default fixed chunking
        };
    }

    private static string DetermineEmbeddingProvider(string? documentType, long fileSize, Dictionary<string, object>? metadata)
    {
        // Check for explicit provider in metadata
        if (metadata?.ContainsKey("embeddingProvider") == true)
        {
            return metadata["embeddingProvider"]?.ToString() ?? "sentence-transformers";
        }

        // For now, use sentence-transformers for everything (free, high quality)
        // Later we can add rules like:
        // - Legal/medical -> OpenAI (highest quality)
        // - Large documents -> spaCy (faster, lower dimensional)
        // - Multilingual -> Cohere

        return documentType switch
        {
            "legal" => "sentence-transformers", // Could be "openai" if budget allows
            "medical" => "sentence-transformers", // Could be "openai" if budget allows
            "code" => "sentence-transformers", // Good for code understanding
            _ => "sentence-transformers" // Default: free, high quality
        };
    }

    private static bool DetermineNERRequirement(string? documentType, long fileSize)
    {
        // Enable NER for documents that typically contain entities
        return documentType switch
        {
            "legal" => true,
            "medical" => true,
            "technical" => false,
            "code" => false,
            "structured" => false,
            _ => fileSize < 1_000_000 // Enable for smaller general documents
        };
    }

    private static string DeterminePriority(long fileSize, string? documentType)
    {
        // Small files = high priority (quick to process)
        if (fileSize < 50_000) return "high";

        // Critical document types
        if (documentType is "legal" or "medical") return "high";

        // Large files = low priority (don't block queue)
        if (fileSize > 5_000_000) return "low";

        return "normal";
    }

    private static decimal EstimateProcessingCost(PipelinePlan plan)
    {
        decimal cost = 0;

        // OCR is expensive
        if (plan.RequiresOCR) cost += 10;

        // Embedding costs vary by provider
        cost += plan.EmbeddingProvider switch
        {
            "openai" => 5.0m,
            "cohere" => 3.0m,
            "sentence-transformers" => 0.1m,
            "spacy" => 0.05m,
            _ => 0.1m
        };

        // NER processing
        if (plan.RequiresNER) cost += 2.0m;

        // Reranking
        if (plan.UseReranking) cost += 3.0m;

        return cost;
    }

    private async Task<string> DetectLanguageWithFallbackAsync(string? contentPreview)
    {
        if (string.IsNullOrWhiteSpace(contentPreview)) return "en";

        try
        {
            var detection = await _languageDetectionService.DetectLanguageAsync(contentPreview, 0.0f);
            // Prefer ISO 639-1 if available
            if (!string.IsNullOrWhiteSpace(detection.IsoCode639_1))
            {
                return detection.IsoCode639_1.ToLowerInvariant();
            }

            // Fallback: map common language names to codes
            return detection.LanguageName.ToLowerInvariant() switch
            {
                "english" => "en",
                "spanish" => "es",
                "french" => "fr",
                "german" => "de",
                "italian" => "it",
                "russian" => "ru",
                "chinese" => "zh",
                "japanese" => "ja",
                "korean" => "ko",
                "arabic" => "ar",
                _ => "en"
            };
        }
        catch
        {
            // Microservice unavailable: fall back to basic heuristic
            if (MyRegex().IsMatch(contentPreview)) return "zh"; // Chinese
            if (Regex.IsMatch(contentPreview, @"[\u0400-\u04FF]")) return "ru"; // Russian
            if (Regex.IsMatch(contentPreview, @"[\u0600-\u06FF]")) return "ar"; // Arabic
            if (Regex.IsMatch(contentPreview, @"[\u3040-\u309F]")) return "ja"; // Japanese hiragana
            if (Regex.IsMatch(contentPreview, @"[\uAC00-\uD7AF]")) return "ko"; // Korean

            var lowerContent = contentPreview.ToLowerInvariant();
            if (lowerContent.Contains(" el ") || lowerContent.StartsWith("el ") || lowerContent.Contains(" la ") || lowerContent.Contains(" los ")) return "es";
            if (lowerContent.Contains(" le ") || lowerContent.StartsWith("le ") || lowerContent.Contains(" les ")) return "fr";
            if (lowerContent.Contains(" der ") || lowerContent.Contains(" die ") || lowerContent.Contains(" das ")) return "de";

            return "en";
        }
    }

    [GeneratedRegex(@"[\u4E00-\u9FFF]")]
    private static partial Regex MyRegex();
}
