using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace SmartCollectAPI.Services.Providers;

public class OssDocumentParser(
    ILogger<OssDocumentParser> logger,
    PdfPigParser pdfParser,
    ILibreOfficeConversionService conversionService) : IAdvancedDocumentParser
{
    private readonly ILogger<OssDocumentParser> _logger = logger;
    private readonly PdfPigParser _pdfParser = pdfParser;
    private readonly ILibreOfficeConversionService _conversionService = conversionService;

    private static readonly HashSet<string> _pdfMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf"
    };

    public bool CanHandle(string mimeType)
    {
        var normalized = mimeType.ToLowerInvariant();
        return _pdfMimeTypes.Contains(normalized) || _conversionService.CanConvert(normalized);
    }

    public async Task<DocumentParseResult> ParseAsync(Stream documentStream, string mimeType, CancellationToken cancellationToken = default)
    {
        var normalized = mimeType.ToLowerInvariant();
        await using var buffer = new MemoryStream();
        await documentStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        if (_pdfMimeTypes.Contains(normalized))
        {
            _logger.LogInformation("Parsing PDF document via PdfPig parser.");
            return await _pdfParser.ParseAsync(buffer, "application/pdf", cancellationToken);
        }

        if (_conversionService.IsEnabled && _conversionService.CanConvert(normalized))
        {
            buffer.Position = 0;
            var convertedStream = await _conversionService.ConvertToPdfAsync(buffer, normalized, cancellationToken);
            if (convertedStream != null)
            {
                await using (convertedStream)
                {
                    _logger.LogInformation("Converted {MimeType} to PDF using LibreOffice. Delegating to PdfPig parser.", mimeType);
                    var parseResult = await _pdfParser.ParseAsync(convertedStream, "application/pdf", cancellationToken);
                    var metadata = parseResult.Metadata ?? [];
                    metadata["convertedFrom"] = normalized;
                    metadata["conversionTool"] = "LibreOffice";
                    metadata["originalMimeType"] = normalized;
                    return parseResult with { Metadata = metadata };
                }
            }
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, leaveOpen: true);
        var fallbackText = await reader.ReadToEndAsync(cancellationToken);
        _logger.LogWarning("Falling back to plain-text extraction for MIME type {MimeType}.", mimeType);

        var metadataFallback = new Dictionary<string, object>
        {
            ["parser"] = "PlainTextFallback",
            ["originalMimeType"] = normalized,
            ["conversionAttempted"] = _conversionService.IsEnabled && _conversionService.CanConvert(normalized)
        };

        return new DocumentParseResult(
            ExtractedText: fallbackText,
            Entities: [],
            Tables: [],
            Sections: [],
            Metadata: metadataFallback,
            Success: true,
            ErrorMessage: null
        );
    }
}
