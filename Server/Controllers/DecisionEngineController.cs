using Microsoft.AspNetCore.Mvc;
using SmartCollectAPI.Services.Pipeline;
using SmartCollectAPI.Services.Providers;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DecisionEngineController(
    IDecisionEngine decisionEngine,
    IEmbeddingProviderFactory embeddingFactory,
    ILogger<DecisionEngineController> logger) : ControllerBase
{
    private readonly IDecisionEngine _decisionEngine = decisionEngine;
    private readonly IEmbeddingProviderFactory _embeddingFactory = embeddingFactory;
    private readonly ILogger<DecisionEngineController> _logger = logger;

    /// <summary>
    /// Test the decision engine with sample document metadata
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<PipelinePlan>> AnalyzeDocument([FromBody] DocumentAnalysisRequest request)
    {
        try
        {
            var plan = await _decisionEngine.GeneratePlanAsync(
                request.FileName,
                request.FileSize,
                request.MimeType,
                request.ContentPreview,
                request.Metadata);

            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze document");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get sample test cases for the decision engine
    /// </summary>
    [HttpGet("test-cases")]
    public ActionResult<List<DocumentAnalysisRequest>> GetTestCases()
    {
        var testCases = new List<DocumentAnalysisRequest>
        {
            new()
            {
                FileName = "Contract_2024.pdf",
                FileSize = 150_000,
                MimeType = "application/pdf",
                ContentPreview = "AGREEMENT\n\nWHEREAS the parties hereinafter agree to the following terms..."
            },
            new()
            {
                FileName = "patient_record.txt",
                FileSize = 50_000,
                MimeType = "text/plain",
                ContentPreview = "Patient Name: John Doe\nDiagnosis: Type 2 Diabetes\nPrescription: Metformin 500mg..."
            },
            new()
            {
                FileName = "api_documentation.md",
                FileSize = 200_000,
                MimeType = "text/markdown",
                ContentPreview = "# API Documentation\n\n## Authentication\n\nThis API uses OAuth2..."
            },
            new()
            {
                FileName = "image_scan.jpg",
                FileSize = 2_000_000,
                MimeType = "image/jpeg"
            },
            new()
            {
                FileName = "app.py",
                FileSize = 30_000,
                MimeType = "text/x-python",
                ContentPreview = "from fastapi import FastAPI\n\napp = FastAPI()\n\n@app.get('/')\nasync def root()..."
            },
            new()
            {
                FileName = "data.json",
                FileSize = 500_000,
                MimeType = "application/json",
                ContentPreview = "{\"users\": [{\"id\": 1, \"name\": \"Alice\"}, {\"id\": 2, \"name\": \"Bob\"}]}"
            },
            new()
            {
                FileName = "large_document.txt",
                FileSize = 10_000_000,
                MimeType = "text/plain",
                ContentPreview = "This is a very large document with lots of content..."
            }
        };

        return Ok(testCases);
    }

    /// <summary>
    /// Run all test cases and compare results
    /// </summary>
    [HttpGet("run-tests")]
    public async Task<ActionResult<List<TestResult>>> RunTests()
    {
        var testCases = GetTestCases().Value ?? [];
        var results = new List<TestResult>();

        foreach (var testCase in testCases)
        {
            try
            {
                var plan = await _decisionEngine.GeneratePlanAsync(
                    testCase.FileName,
                    testCase.FileSize,
                    testCase.MimeType,
                    testCase.ContentPreview,
                    testCase.Metadata);

                results.Add(new TestResult
                {
                    FileName = testCase.FileName,
                    Success = true,
                    Plan = plan
                });
            }
            catch (Exception ex)
            {
                results.Add(new TestResult
                {
                    FileName = testCase.FileName,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        return Ok(results);
    }

    /// <summary>
    /// Get list of available embedding providers
    /// </summary>
    [HttpGet("providers")]
    public ActionResult<ProviderInfoResponse> GetAvailableProviders()
    {
        var providers = _embeddingFactory.GetAvailableProviders();
        var providerDetails = new List<ProviderDetail>();

        foreach (var providerKey in providers)
        {
            try
            {
                var service = _embeddingFactory.GetProvider(providerKey);
                providerDetails.Add(new ProviderDetail
                {
                    Key = providerKey,
                    Dimensions = service.EmbeddingDimensions,
                    MaxTokens = service.MaxTokens,
                    Available = true
                });
            }
            catch (Exception ex)
            {
                providerDetails.Add(new ProviderDetail
                {
                    Key = providerKey,
                    Available = false,
                    Error = ex.Message
                });
            }
        }

        return Ok(new ProviderInfoResponse
        {
            DefaultProvider = "sentence-transformers",
            Providers = providerDetails
        });
    }

    /// <summary>
    /// Test embedding generation with a specific provider
    /// </summary>
    [HttpPost("test-provider")]
    public async Task<ActionResult<EmbeddingTestResult>> TestProvider([FromBody] TestProviderRequest request)
    {
        try
        {
            if (!_embeddingFactory.IsProviderSupported(request.ProviderKey))
            {
                return BadRequest(new
                {
                    error = $"Provider '{request.ProviderKey}' is not supported",
                    availableProviders = _embeddingFactory.GetAvailableProviders()
                });
            }

            var provider = _embeddingFactory.GetProvider(request.ProviderKey);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var result = await provider.GenerateEmbeddingAsync(request.Text);
            stopwatch.Stop();

            return Ok(new EmbeddingTestResult
            {
                ProviderKey = request.ProviderKey,
                Success = result.Success,
                Dimensions = result.Embedding.ToArray().Length,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = result.ErrorMessage,
                SampleValues = result.Success ? [.. result.Embedding.ToArray().Take(5)] : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test provider {ProviderKey}", request.ProviderKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Compare embedding generation across all providers
    /// </summary>
    [HttpPost("compare-providers")]
    public async Task<ActionResult<List<EmbeddingTestResult>>> CompareProviders([FromBody] CompareProvidersRequest request)
    {
        var results = new List<EmbeddingTestResult>();

        foreach (var providerKey in _embeddingFactory.GetAvailableProviders())
        {
            try
            {
                var provider = _embeddingFactory.GetProvider(providerKey);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var result = await provider.GenerateEmbeddingAsync(request.Text);
                stopwatch.Stop();

                results.Add(new EmbeddingTestResult
                {
                    ProviderKey = providerKey,
                    Success = result.Success,
                    Dimensions = result.Embedding.ToArray().Length,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = result.ErrorMessage,
                    SampleValues = result.Success ? [.. result.Embedding.ToArray().Take(5)] : null
                });
            }
            catch (Exception ex)
            {
                results.Add(new EmbeddingTestResult
                {
                    ProviderKey = providerKey,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return Ok(results);
    }
}

public class DocumentAnalysisRequest
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string? ContentPreview { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class TestResult
{
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public PipelinePlan? Plan { get; set; }
    public string? Error { get; set; }
}

public class ProviderInfoResponse
{
    public string DefaultProvider { get; set; } = string.Empty;
    public List<ProviderDetail> Providers { get; set; } = [];
}

public class ProviderDetail
{
    public string Key { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public int MaxTokens { get; set; }
    public bool Available { get; set; }
    public string? Error { get; set; }
}

public class TestProviderRequest
{
    public string ProviderKey { get; set; } = string.Empty;
    public string Text { get; set; } = "This is a test sentence for embedding generation.";
}

public class CompareProvidersRequest
{
    public string Text { get; set; } = "This is a test sentence for embedding generation.";
}

public class EmbeddingTestResult
{
    public string ProviderKey { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int Dimensions { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public List<float>? SampleValues { get; set; }
}
