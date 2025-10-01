namespace SmartCollectAPI.Models;

/// <summary>
/// Represents a processing plan for a document through the pipeline.
/// Contains decisions about chunking, embedding, and processing strategies.
/// </summary>
public class PipelinePlan
{
    /// <summary>
    /// Chunking strategy to use: "semantic", "fixed", "markdown", "sentence", "paragraph"
    /// </summary>
    public string ChunkingStrategy { get; set; } = "fixed";

    /// <summary>
    /// Size of each chunk in characters (for fixed chunking)
    /// </summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between chunks in characters
    /// </summary>
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>
    /// Embedding provider to use: "sentence-transformers", "spacy", "openai", "cohere"
    /// </summary>
    public string EmbeddingProvider { get; set; } = "sentence-transformers";

    /// <summary>
    /// Detected or specified language code (ISO 639-1)
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Whether OCR is required for this document
    /// </summary>
    public bool RequiresOCR { get; set; } = false;

    /// <summary>
    /// Whether NER (Named Entity Recognition) should be performed
    /// </summary>
    public bool RequiresNER { get; set; } = true;

    /// <summary>
    /// Whether to use reranking for search results
    /// </summary>
    public bool UseReranking { get; set; } = false;

    /// <summary>
    /// Document type hint for specialized processing
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// Priority level: "low", "normal", "high", "critical"
    /// </summary>
    public string Priority { get; set; } = "normal";

    /// <summary>
    /// Estimated processing cost (arbitrary units)
    /// </summary>
    public decimal EstimatedCost { get; set; } = 0;

    /// <summary>
    /// Reasoning behind the plan decisions (for debugging/auditing)
    /// </summary>
    public List<string> DecisionReasons { get; set; } = new();

    /// <summary>
    /// Additional metadata for the plan
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
