using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services.Providers;

public interface IAdvancedDocumentParser
{
    Task<DocumentParseResult> ParseAsync(Stream documentStream, string mimeType, CancellationToken cancellationToken = default);
    bool CanHandle(string mimeType);
}

public record DocumentParseResult(
    string ExtractedText,
    List<ExtractedEntity>? Entities = null,
    List<ExtractedTable>? Tables = null,
    List<DocumentSection>? Sections = null,
    Dictionary<string, object>? Metadata = null,
    bool Success = true,
    string? ErrorMessage = null
);

public record ExtractedEntity(
    string Name,
    string Type,
    double Salience,
    List<EntityMention>? Mentions = null
);

public record EntityMention(
    string Text,
    int StartOffset,
    int EndOffset
);

public record ExtractedTable(
    int RowCount,
    int ColumnCount,
    List<List<string>> Rows,
    Dictionary<string, object>? Metadata = null
);

public record DocumentSection(
    string Title,
    string Content,
    int PageNumber,
    Dictionary<string, object>? Metadata = null
);