using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace SmartCollectAPI.Services.Repositories;

public class StagingRepository : IStagingRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public StagingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Guid> InsertAsync(string jobId, string sourceUri, string? mime, string? sha256, JsonNode? rawMetadata, string status, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        const string sql = @"INSERT INTO staging_documents (job_id, source_uri, mime, sha256, raw_metadata, status)
                             VALUES ($1, $2, $3, $4, $5::jsonb, $6)
                             RETURNING id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(jobId);
        cmd.Parameters.AddWithValue(sourceUri);
        cmd.Parameters.AddWithValue((object?)mime ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)sha256 ?? DBNull.Value);
        cmd.Parameters.AddWithValue(rawMetadata is null ? (object)DBNull.Value : JsonSerializer.Serialize(rawMetadata));
        cmd.Parameters.AddWithValue(status);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return id;
    }

    public async Task<int> UpdateStatusAsync(string jobId, string status, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        const string sql = @"UPDATE staging_documents SET status = $2, updated_at = NOW() WHERE job_id = $1;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(jobId);
        cmd.Parameters.AddWithValue(status);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows;
    }
}
