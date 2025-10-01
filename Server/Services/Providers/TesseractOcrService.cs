using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartCollectAPI.Services.Providers;

public class TesseractOptions
{
    public bool Enabled { get; set; } = true;
    public string? BinaryPath { get; set; } = "tesseract";
    public string Languages { get; set; } = "eng";
    public int TimeoutSeconds { get; set; } = 90;
    public int? PageSegmentationMode { get; set; }
}

public class TesseractOcrService : IOcrService
{
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly TesseractOptions _options;

    private static readonly Dictionary<string, string> _mimeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/bmp"] = ".bmp",
        ["image/tiff"] = ".tiff",
        ["image/tif"] = ".tif",
        ["application/pdf"] = ".pdf"
    };

    public TesseractOcrService(ILogger<TesseractOcrService> logger, IOptions<TesseractOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public bool CanHandle(string mimeType)
    {
        return _mimeToExtension.ContainsKey(mimeType);
    }

    public async Task<OcrResult> ExtractTextAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Tesseract OCR disabled via configuration.");
            return new OcrResult(
                ExtractedText: string.Empty,
                Success: false,
                ErrorMessage: "Tesseract OCR is disabled."
            );
        }

        if (!CanHandle(mimeType))
        {
            _logger.LogWarning("Tesseract OCR does not support MIME type {MimeType}.", mimeType);
            return new OcrResult(
                ExtractedText: string.Empty,
                Success: false,
                ErrorMessage: $"Unsupported MIME type: {mimeType}"
            );
        }

        var extension = _mimeToExtension[mimeType];
        var tempRoot = Path.Combine(Path.GetTempPath(), "smartcollect", "tesseract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var inputPath = Path.Combine(tempRoot, "image" + extension);
        var outputBase = Path.Combine(tempRoot, "output");
        var timeout = TimeSpan.FromSeconds(Math.Max(10, _options.TimeoutSeconds));
        var binaryPath = string.IsNullOrWhiteSpace(_options.BinaryPath) ? "tesseract" : _options.BinaryPath;

        try
        {
            if (imageStream.CanSeek)
            {
                imageStream.Position = 0;
            }

            await using (var file = File.Create(inputPath))
            {
                await imageStream.CopyToAsync(file, cancellationToken);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add(outputBase);

            if (!string.IsNullOrWhiteSpace(_options.Languages))
            {
                startInfo.ArgumentList.Add("-l");
                startInfo.ArgumentList.Add(_options.Languages);
            }

            if (_options.PageSegmentationMode.HasValue)
            {
                startInfo.ArgumentList.Add("--psm");
                startInfo.ArgumentList.Add(_options.PageSegmentationMode.Value.ToString());
            }

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            _logger.LogInformation("Running Tesseract OCR for {InputPath} using {Binary}", inputPath, binaryPath);
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start tesseract process.");
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch (Exception killEx)
                {
                    _logger.LogWarning(killEx, "Failed to terminate timed-out tesseract process.");
                }

                throw new TimeoutException($"Tesseract OCR timed out after {timeout.TotalSeconds} seconds.");
            }

            var stderr = await process.StandardError.ReadToEndAsync();
            var stdout = await process.StandardOutput.ReadToEndAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Tesseract OCR failed with exit code {process.ExitCode}. StdOut: {stdout}. StdErr: {stderr}");
            }

            var textPath = outputBase + ".txt";
            if (!File.Exists(textPath))
            {
                throw new FileNotFoundException("Tesseract OCR completed but no text output was produced.");
            }

            var text = await File.ReadAllTextAsync(textPath, cancellationToken);

            return new OcrResult(
                ExtractedText: text,
                Annotations: new List<TextAnnotation>(),
                Objects: new List<DetectedObject>(),
                Success: true,
                ErrorMessage: null
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not TimeoutException)
        {
            _logger.LogWarning(ex, "Tesseract OCR failed for {MimeType}.", mimeType);
            return new OcrResult(
                ExtractedText: string.Empty,
                Success: false,
                ErrorMessage: ex.Message
            );
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogDebug(cleanupEx, "Failed to clean Tesseract temp directory {TempRoot}.", tempRoot);
            }
        }
    }
}
