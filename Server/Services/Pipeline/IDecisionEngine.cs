using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services.Pipeline;

/// <summary>
/// Decision engine that analyzes documents and generates optimal processing plans.
/// Uses rule-based logic to determine chunking strategies, embedding providers,
/// and other processing parameters.
/// </summary>
public interface IDecisionEngine
{
    /// <summary>
    /// Generate a processing plan for a document based on its metadata and content.
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <param name="fileSize">Size of the file in bytes</param>
    /// <param name="mimeType">MIME type of the file</param>
    /// <param name="contentPreview">First few KB of content for analysis</param>
    /// <param name="metadata">Additional metadata about the document</param>
    /// <returns>A complete processing plan</returns>
    Task<PipelinePlan> GeneratePlanAsync(
        string fileName,
        long fileSize,
        string mimeType,
        string? contentPreview = null,
        Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Generate a plan for a StagingDocument
    /// </summary>
    Task<PipelinePlan> GeneratePlanForStagingAsync(StagingDocument document);
}
