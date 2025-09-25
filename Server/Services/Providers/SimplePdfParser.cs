using Microsoft.Extensions.Logging;
using System.Text;

namespace SmartCollectAPI.Services.Providers;

public class SimplePdfParser : IAdvancedDocumentParser
{
    private readonly ILogger<SimplePdfParser> _logger;

    private readonly HashSet<string> _supportedMimeTypes = new()
    {
        "application/pdf"
    };

    public SimplePdfParser(ILogger<SimplePdfParser> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(string mimeType)
    {
        return _supportedMimeTypes.Contains(mimeType.ToLowerInvariant());
    }

    public async Task<DocumentParseResult> ParseAsync(Stream documentStream, string mimeType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing PDF with simple parser (fallback implementation)");

            // This is a basic implementation that would need a proper PDF library
            // For now, we'll return a basic result indicating PDF processing is needed
            await Task.Delay(100, cancellationToken); // Simulate processing time

            var extractedText = "PDF parsing requires advanced library integration. " +
                               "Consider using Google Document AI for full PDF processing capabilities.";

            var metadata = new Dictionary<string, object>
            {
                ["parser"] = "SimplePdfParser",
                ["note"] = "Fallback implementation - limited functionality"
            };

            return new DocumentParseResult(
                ExtractedText: extractedText,
                Entities: new List<ExtractedEntity>(),
                Tables: new List<ExtractedTable>(),
                Sections: new List<DocumentSection>
                {
                    new("Document", extractedText, 1, metadata)
                },
                Metadata: metadata,
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PDF with simple parser");
            return new DocumentParseResult(
                ExtractedText: string.Empty,
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }
}