using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCollectAPI.Data;

namespace SmartCollectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticController : ControllerBase
{
    private readonly SmartCollectDbContext _context;

    public DiagnosticController(SmartCollectDbContext context)
    {
        _context = context;
    }

    [HttpGet("hash-comparison")]
    public async Task<IActionResult> GetHashComparison()
    {
        // Get staging done document hashes
        var stagingDoneHashes = await _context.StagingDocuments
            .Where(d => d.Status == "done" && d.Sha256 != null)
            .Select(d => new { d.Id, d.JobId, d.Sha256, d.SourceUri })
            .ToListAsync();

        // Get final document hashes
        var finalDocHashes = await _context.Documents
            .Where(d => d.Sha256 != null)
            .Select(d => new { d.Id, d.Sha256, d.SourceUri })
            .ToListAsync();

        var stagingHashSet = stagingDoneHashes.Select(d => d.Sha256).ToHashSet();
        var finalHashSet = finalDocHashes.Select(d => d.Sha256).ToHashSet();

        var missingInFinal = stagingDoneHashes
            .Where(d => !finalHashSet.Contains(d.Sha256))
            .ToList();

        return Ok(new
        {
            StagingDoneDocuments = stagingDoneHashes,
            FinalDocuments = finalDocHashes,
            MissingInFinal = missingInFinal,
            Counts = new
            {
                StagingDone = stagingDoneHashes.Count,
                Final = finalDocHashes.Count,
                Missing = missingInFinal.Count
            }
        });
    }

    [HttpPost("fix-root-cause")]
    public IActionResult FixRootCause()
    {
        return Ok(new
        {
            Message = "Fixed IngestWorker.cs - removed manual Id assignment to allow auto-generation",
            Issue = "Documents table has ValueGeneratedOnAdd() for Id, but IngestWorker was setting Id = job.JobId",
            Solution = "Let Entity Framework auto-generate unique Document IDs",
            NextStep = "Restart server to load updated code"
        });
    }
}