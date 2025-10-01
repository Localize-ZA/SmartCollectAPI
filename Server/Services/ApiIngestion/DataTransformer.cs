using System.Text.Json;
using System.Text.RegularExpressions;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services.ApiIngestion;

public interface IDataTransformer
{
    Task<List<TransformedDocument>> TransformAsync(
        ApiSource source, 
        ApiResponse response, 
        CancellationToken cancellationToken = default);
}

public class TransformedDocument
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Description { get; set; }
    public string? Source { get; set; }
    public string? SourceUrl { get; set; }
    public string? FileType { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class DataTransformer : IDataTransformer
{
    private readonly ILogger<DataTransformer> _logger;

    public DataTransformer(ILogger<DataTransformer> logger)
    {
        _logger = logger;
    }

    public async Task<List<TransformedDocument>> TransformAsync(
        ApiSource source,
        ApiResponse response,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TransformedDocument>();

        if (!response.Success || string.IsNullOrEmpty(response.RawResponse))
        {
            _logger.LogWarning("Cannot transform unsuccessful or empty response from {SourceName}", source.Name);
            return results;
        }

        try
        {
            // Parse the raw JSON response
            using var document = JsonDocument.Parse(response.RawResponse);
            var root = document.RootElement;

            // Extract records using response path (JSONPath-like)
            var records = ExtractRecords(root, source.ResponsePath ?? "$");

            _logger.LogInformation(
                "Extracted {Count} records from {SourceName} using path {Path}",
                records.Count,
                source.Name,
                source.ResponsePath
            );

            // Parse field mappings
            var fieldMappings = ParseFieldMappings(source.FieldMappings);

            // Transform each record
            foreach (var record in records)
            {
                var transformed = TransformRecord(record, fieldMappings, source);
                if (transformed != null)
                {
                    results.Add(transformed);
                }
            }

            response.RecordCount = results.Count;

            _logger.LogInformation(
                "Successfully transformed {Count} documents from {SourceName}",
                results.Count,
                source.Name
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response from {SourceName}", source.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transform data from {SourceName}", source.Name);
            throw;
        }

        await Task.CompletedTask; // For async consistency
        return results;
    }

    private List<JsonElement> ExtractRecords(JsonElement root, string path)
    {
        var records = new List<JsonElement>();

        // Simple JSONPath implementation
        // Supports: $ (root), $[*] (all array items), $.data, $.items[*], etc.
        
        if (string.IsNullOrEmpty(path) || path == "$")
        {
            // Root is the record(s)
            if (root.ValueKind == JsonValueKind.Array)
            {
                records.AddRange(root.EnumerateArray());
            }
            else
            {
                records.Add(root);
            }
            return records;
        }

        // Remove leading $. or $
        path = path.TrimStart('$').TrimStart('.');

        if (string.IsNullOrEmpty(path))
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                records.AddRange(root.EnumerateArray());
            }
            else
            {
                records.Add(root);
            }
            return records;
        }

        // Split path into segments
        var segments = path.Split('.');
        var current = root;

        foreach (var segment in segments)
        {
            // Handle array notation like "items[*]" or "items"
            var arrayMatch = Regex.Match(segment, @"^(\w+)\[\*\]$");
            
            if (arrayMatch.Success)
            {
                // Navigate to array and expand
                var arrayName = arrayMatch.Groups[1].Value;
                if (current.TryGetProperty(arrayName, out var arrayElement) && 
                    arrayElement.ValueKind == JsonValueKind.Array)
                {
                    records.AddRange(arrayElement.EnumerateArray());
                    return records; // Array expansion is terminal
                }
                return records; // Property not found or not array
            }
            else
            {
                // Navigate to property
                if (current.TryGetProperty(segment, out var property))
                {
                    current = property;
                }
                else
                {
                    return records; // Property not found
                }
            }
        }

        // After navigating, check if final element is array
        if (current.ValueKind == JsonValueKind.Array)
        {
            records.AddRange(current.EnumerateArray());
        }
        else
        {
            records.Add(current);
        }

        return records;
    }

    private Dictionary<string, string> ParseFieldMappings(string? fieldMappingsJson)
    {
        if (string.IsNullOrEmpty(fieldMappingsJson))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(fieldMappingsJson)
                ?? new Dictionary<string, string>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse field mappings, using defaults");
            return new Dictionary<string, string>();
        }
    }

    private TransformedDocument? TransformRecord(
        JsonElement record,
        Dictionary<string, string> fieldMappings,
        ApiSource source)
    {
        try
        {
            var doc = new TransformedDocument
            {
                Source = source.Name,
                SourceUrl = source.EndpointUrl,
                FileType = "json",
                Metadata = new Dictionary<string, object>()
            };

            // Apply field mappings
            doc.Title = GetFieldValue(record, fieldMappings, "title", "name", "subject", "headline");
            doc.Content = GetFieldValue(record, fieldMappings, "content", "body", "text", "description");
            doc.Description = GetFieldValue(record, fieldMappings, "description", "summary", "excerpt");

            // Extract published date if available
            var publishedStr = GetFieldValue(record, fieldMappings, "published_at", "publishedAt", "createdAt", "created_at", "date");
            if (!string.IsNullOrEmpty(publishedStr) && DateTime.TryParse(publishedStr, out var publishedDate))
            {
                doc.PublishedAt = publishedDate;
            }

            // Store all fields in metadata for reference
            foreach (var property in record.EnumerateObject())
            {
                try
                {
                    doc.Metadata[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? "",
                        JsonValueKind.Number => property.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => "",
                        _ => property.Value.ToString()
                    };
                }
                catch
                {
                    // Skip properties that can't be converted
                }
            }

            // Validate minimum requirements
            if (string.IsNullOrEmpty(doc.Title) && string.IsNullOrEmpty(doc.Content))
            {
                _logger.LogWarning("Skipping record with no title or content");
                return null;
            }

            // If no content, use title as content
            if (string.IsNullOrEmpty(doc.Content))
            {
                doc.Content = doc.Title;
            }

            // If no title, create from content
            if (string.IsNullOrEmpty(doc.Title))
            {
                doc.Title = doc.Content?.Length > 100 
                    ? doc.Content[..100] + "..." 
                    : doc.Content;
            }

            return doc;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to transform individual record");
            return null;
        }
    }

    private string? GetFieldValue(
        JsonElement record,
        Dictionary<string, string> fieldMappings,
        params string[] fieldNames)
    {
        // First check explicit field mappings
        foreach (var fieldName in fieldNames)
        {
            if (fieldMappings.TryGetValue(fieldName, out var mappedField))
            {
                if (record.TryGetProperty(mappedField, out var mappedValue) &&
                    mappedValue.ValueKind == JsonValueKind.String)
                {
                    return mappedValue.GetString();
                }
            }
        }

        // Then try default field names
        foreach (var fieldName in fieldNames)
        {
            if (record.TryGetProperty(fieldName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }
}
