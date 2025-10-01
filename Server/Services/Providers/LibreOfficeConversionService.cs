using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartCollectAPI.Services.Providers;

public interface ILibreOfficeConversionService
{
    bool IsEnabled { get; }
    bool CanConvert(string mimeType);
    Task<Stream?> ConvertToPdfAsync(Stream sourceStream, string mimeType, CancellationToken cancellationToken = default);
}

public class LibreOfficeOptions
{
    public bool Enabled { get; set; } = true;
    public string? BinaryPath { get; set; } = "soffice";
    public int TimeoutSeconds { get; set; } = 90;
}

public class LibreOfficeConversionService(ILogger<LibreOfficeConversionService> logger, IOptions<LibreOfficeOptions> options) : ILibreOfficeConversionService
{
    private readonly ILogger<LibreOfficeConversionService> _logger = logger;
    private readonly LibreOfficeOptions _options = options.Value;

    private static readonly Dictionary<string, string> _mimeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.ms-word.document.macroenabled.12"] = ".docm",
        ["application/vnd.ms-excel"] = ".xls",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["application/vnd.ms-excel.sheet.macroenabled.12"] = ".xlsm",
        ["application/vnd.ms-powerpoint"] = ".ppt",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx",
        ["application/vnd.ms-powerpoint.presentation.macroenabled.12"] = ".pptm",
        ["text/rtf"] = ".rtf",
        ["application/rtf"] = ".rtf",
        ["application/vnd.oasis.opendocument.text"] = ".odt",
        ["application/vnd.oasis.opendocument.spreadsheet"] = ".ods",
        ["application/vnd.oasis.opendocument.presentation"] = ".odp"
    };

    public bool IsEnabled => _options.Enabled;

    public bool CanConvert(string mimeType)
    {
        return IsEnabled && _mimeToExtension.ContainsKey(mimeType);
    }

    public async Task<Stream?> ConvertToPdfAsync(Stream sourceStream, string mimeType, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("LibreOffice conversion skipped because it is disabled.");
            return null;
        }

        if (!CanConvert(mimeType))
        {
            _logger.LogDebug("LibreOffice conversion skipped because MIME type {MimeType} is not supported.", mimeType);
            return null;
        }

        var normalizedMime = mimeType.ToLowerInvariant();
        if (!_mimeToExtension.TryGetValue(normalizedMime, out var extension))
        {
            _logger.LogDebug("LibreOffice conversion skipped due to unknown extension for MIME type {MimeType}.", mimeType);
            return null;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "smartcollect", "lo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var inputPath = Path.Combine(tempRoot, "source" + extension);
        var outputDirectory = tempRoot;
        var timeout = TimeSpan.FromSeconds(Math.Max(10, _options.TimeoutSeconds));
        var binaryPath = string.IsNullOrWhiteSpace(_options.BinaryPath) ? "soffice" : _options.BinaryPath;

        try
        {
            if (sourceStream.CanSeek)
            {
                sourceStream.Position = 0;
            }

            await using (var file = File.Create(inputPath))
            {
                await sourceStream.CopyToAsync(file, cancellationToken);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("--headless");
            startInfo.ArgumentList.Add("--norestore");
            startInfo.ArgumentList.Add("--convert-to");
            startInfo.ArgumentList.Add("pdf");
            startInfo.ArgumentList.Add("--outdir");
            startInfo.ArgumentList.Add(outputDirectory);
            startInfo.ArgumentList.Add(inputPath);

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            _logger.LogInformation("Launching LibreOffice conversion for {InputPath} using {BinaryPath}.", inputPath, binaryPath);
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start LibreOffice process.");
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
                    _logger.LogWarning(killEx, "Failed to terminate timed-out LibreOffice process.");
                }

                throw new TimeoutException($"LibreOffice conversion timed out after {timeout.TotalSeconds} seconds.");
            }

            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"LibreOffice conversion failed with exit code {process.ExitCode}. StdOut: {stdout}. StdErr: {stderr}");
            }

            var outputPath = Directory.EnumerateFiles(outputDirectory, "*.pdf", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetCreationTimeUtc)
                .FirstOrDefault();

            if (outputPath is null || !File.Exists(outputPath))
            {
                throw new FileNotFoundException("LibreOffice conversion completed but no PDF output was produced.");
            }

            _logger.LogInformation("LibreOffice conversion produced {OutputPath}.", outputPath);

            var pdfBytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            var memoryStream = new MemoryStream(pdfBytes, writable: false)
            {
                Position = 0
            };
            return memoryStream;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not TimeoutException)
        {
            _logger.LogWarning(ex, "LibreOffice conversion failed for {MimeType}.", mimeType);
            return null;
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
                _logger.LogDebug(cleanupEx, "Failed to clean LibreOffice temp directory {TempRoot}.", tempRoot);
            }
        }
    }
}
