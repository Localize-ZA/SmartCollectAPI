using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCollectAPI.Data;
using SmartCollectAPI.Models;
using System.Text.Json;
using Pgvector;

namespace SmartCollectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController(SmartCollectDbContext context, ILogger<DebugController> logger) : ControllerBase
{
    private readonly SmartCollectDbContext _context = context;
    private readonly ILogger<DebugController> _logger = logger;

    [HttpPost("migrate-stuck-documents")]
    public async Task<IActionResult> MigrateStuckDocuments()
    {
        var results = new List<object>();

        try
        {
            // Find documents that are "done" in staging but not in final Documents table
            var stagingDone = await _context.StagingDocuments
                .Where(d => d.Status == "done" && d.Normalized != null && d.Sha256 != null)
                .ToListAsync();

            var finalDocHashes = await _context.Documents
                .Where(d => d.Sha256 != null)
                .Select(d => d.Sha256)
                .ToListAsync();

            var stuckDocuments = stagingDone
                .Where(d => !finalDocHashes.Contains(d.Sha256))
                .ToList();

            _logger.LogInformation("Found {StagingDoneCount} staging done documents, {FinalHashCount} final document hashes", stagingDone.Count, finalDocHashes.Count);
            _logger.LogInformation("Found {Count} stuck documents to migrate", stuckDocuments.Count);

            foreach (var stagingDoc in stuckDocuments)
            {
                try
                {
                    // Extract canonical document from staging
                    var canonicalJson = stagingDoc.Normalized?.ToJsonString();
                    if (string.IsNullOrEmpty(canonicalJson))
                    {
                        results.Add(new { StagingId = stagingDoc.Id, stagingDoc.JobId, Error = "No normalized data found" });
                        continue;
                    }

                    var canonical = JsonSerializer.Deserialize<CanonicalDocument>(canonicalJson);
                    if (canonical == null)
                    {
                        results.Add(new { StagingId = stagingDoc.Id, stagingDoc.JobId, Error = "Failed to deserialize canonical document" });
                        continue;
                    }

                    // Create the Document record with a new GUID
                    var document = new Document
                    {
                        SourceUri = stagingDoc.SourceUri,
                        Mime = stagingDoc.Mime,
                        Sha256 = stagingDoc.Sha256,
                        Canonical = stagingDoc.Normalized!,
                        CreatedAt = stagingDoc.CreatedAt,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        Embedding = null
                    };

                    // Try to set embedding if available
                    if (canonical.EmbeddingDim > 0 && canonical.Embedding != null && canonical.Embedding.Length > 0)
                    {
                        try
                        {
                            document.Embedding = new Vector(canonical.Embedding);
                            _logger.LogInformation("Set embedding with {Dimensions} dimensions for document {JobId}", canonical.EmbeddingDim, stagingDoc.JobId);
                        }
                        catch (Exception embEx)
                        {
                            _logger.LogWarning(embEx, "Failed to set embedding for document {JobId}, proceeding without embedding", stagingDoc.JobId);
                            // Continue without embedding
                        }
                    }

                    // Try to add the document
                    _context.Documents.Add(document);
                    await _context.SaveChangesAsync();

                    results.Add(new
                    {
                        StagingId = stagingDoc.Id,
                        stagingDoc.JobId,
                        Success = true,
                        HasEmbedding = document.Embedding != null,
                        canonical.EmbeddingDim
                    });

                    _logger.LogInformation("Successfully migrated document {JobId}", stagingDoc.JobId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to migrate document {JobId}", stagingDoc.JobId);
                    results.Add(new
                    {
                        StagingId = stagingDoc.Id,
                        stagingDoc.JobId,
                        Error = ex.Message,
                        ExceptionType = ex.GetType().Name
                    });
                }
            }

            return Ok(new
            {
                TotalStuckDocuments = stuckDocuments.Count,
                Results = results,
                Summary = new
                {
                    Successful = results.Count(r => ((dynamic)r).Success == true),
                    Failed = results.Count(r => ((dynamic)r).Error != null)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MigrateStuckDocuments");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}