using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace SmartCollectAPI.Services.Repositories;

public class DocumentsRepository : IDocumentsRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public DocumentsRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Guid> UpsertAsync(string sourceUri, string? mime, string? sha256, JsonNode canonical, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        // If sha256 provided, use it for uniqueness; otherwise upsert by source_uri
        string sql;
        if (!string.IsNullOrWhiteSpace(sha256))
        {
            sql = @"INSERT INTO documents (source_uri, mime, sha256, canonical)
                    VALUES ($1, $2, $3, $4::jsonb)
                    ON CONFLICT (sha256) DO UPDATE SET
                      source_uri = EXCLUDED.source_uri,
                      mime = EXCLUDED.mime,
                      canonical = EXCLUDED.canonical,
                      updated_at = NOW()
                    RETURNING id;";
        }
        else
        {
            sql = @"INSERT INTO documents (source_uri, mime, canonical)
                    VALUES ($1, $2, $3::jsonb)
                    ON CONFLICT (source_uri) DO UPDATE SET
                      mime = EXCLUDED.mime,
                      canonical = EXCLUDED.canonical,
                      updated_at = NOW()
                    RETURNING id;";
        }

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(sourceUri);
        cmd.Parameters.AddWithValue((object?)mime ?? DBNull.Value);
        if (!string.IsNullOrWhiteSpace(sha256))
        {
            cmd.Parameters.AddWithValue(sha256!);
            cmd.Parameters.AddWithValue(JsonSerializer.Serialize(canonical));
        }
        else
        {
            cmd.Parameters.AddWithValue(JsonSerializer.Serialize(canonical));
        }

        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return id;
    }
}
