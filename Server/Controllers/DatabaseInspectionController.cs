using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCollectAPI.Data;

namespace SmartCollectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseInspectionController(SmartCollectDbContext context) : ControllerBase
{
    private readonly SmartCollectDbContext _context = context;

    [HttpGet("staging-vs-final")]
    public async Task<IActionResult> GetStagingVsFinalComparison()
    {
        var stagingCount = await _context.StagingDocuments.CountAsync();
        var finalCount = await _context.Documents.CountAsync();
        var stagingDoneCount = await _context.StagingDocuments.CountAsync(d => d.Status == "done");
        var stagingFailedCount = await _context.StagingDocuments.CountAsync(d => d.Status == "failed");
        var stagingPendingCount = await _context.StagingDocuments.CountAsync(d => d.Status == "pending");

        return Ok(new
        {
            StagingCount = stagingCount,
            StagingDoneCount = stagingDoneCount,
            StagingFailedCount = stagingFailedCount,
            StagingPendingCount = stagingPendingCount,
            FinalCount = finalCount,
            MigrationGap = stagingDoneCount - finalCount
        });
    }

    [HttpGet("staging-details")]
    public async Task<IActionResult> GetStagingDocumentsDetails()
    {
        var stagingDone = await _context.StagingDocuments
            .Where(d => d.Status == "done")
            .Select(d => new
            {
                d.Id,
                d.JobId,
                d.SourceUri,
                d.Status,
                d.Sha256,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync();

        var finalDocuments = await _context.Documents
            .Select(d => new
            {
                d.Id,
                d.SourceUri,
                d.Sha256,
                d.CreatedAt
            })
            .ToListAsync();

        var stagingHashes = stagingDone.Select(d => d.Sha256).ToHashSet();
        var finalHashes = finalDocuments.Select(d => d.Sha256).ToHashSet();
        var missingInFinal = stagingDone.Where(d => !finalHashes.Contains(d.Sha256)).ToList();

        return Ok(new
        {
            StagingDoneDocuments = stagingDone,
            FinalDocuments = finalDocuments,
            MissingInFinal = missingInFinal,
            Count = new
            {
                StagingDone = stagingDone.Count,
                Final = finalDocuments.Count,
                Missing = missingInFinal.Count
            }
        });
    }

    [HttpGet("staging-failed")]
    public async Task<IActionResult> GetFailedStagingDocuments()
    {
        var failedDocs = await _context.StagingDocuments
            .Where(d => d.Status == "failed")
            .Select(d => new
            {
                d.Id,
                d.JobId,
                d.SourceUri,
                d.Status,
                d.Attempts,
                d.CreatedAt,
                d.UpdatedAt,
                HasNormalized = d.Normalized != null
            })
            .ToListAsync();

        return Ok(new
        {
            FailedCount = failedDocs.Count,
            FailedDocuments = failedDocs
        });
    }

    [HttpGet("staging-pending")]
    public async Task<IActionResult> GetPendingStagingDocuments()
    {
        var pendingDocs = await _context.StagingDocuments
            .Where(d => d.Status == "pending")
            .Select(d => new
            {
                d.Id,
                d.JobId,
                d.SourceUri,
                d.Status,
                d.Attempts,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            PendingCount = pendingDocs.Count,
            PendingDocuments = pendingDocs
        });
    }

    [HttpGet("staging-done-not-migrated")]
    public async Task<IActionResult> GetDoneButNotMigratedDocuments()
    {
        var stagingDone = await _context.StagingDocuments
            .Where(d => d.Status == "done")
            .ToListAsync();

        var finalDocIds = await _context.Documents
            .Select(d => d.Id)
            .ToListAsync();

        var notMigratedDocs = stagingDone
            .Where(d => Guid.TryParse(d.JobId, out var jobGuid) && !finalDocIds.Contains(jobGuid))
            .Select(d => new
            {
                d.Id,
                d.JobId,
                d.SourceUri,
                d.Status,
                d.Attempts,
                d.CreatedAt,
                d.UpdatedAt,
                HasNormalized = d.Normalized != null
            })
            .ToList();

        return Ok(new
        {
            DoneButNotMigratedCount = notMigratedDocs.Count,
            DoneButNotMigratedDocuments = notMigratedDocs
        });
    }

    [HttpGet("migration-gaps")]
    public async Task<IActionResult> GetMigrationGaps()
    {
        var stagingDoneCount = await _context.StagingDocuments.CountAsync(d => d.Status == "done");
        var finalDocsCount = await _context.Documents.CountAsync();

        return Ok(new
        {
            TotalStagingDone = stagingDoneCount,
            TotalFinalDocs = finalDocsCount,
            MigrationGaps = stagingDoneCount - finalDocsCount
        });
    }
}