using Microsoft.Data.Sqlite;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public sealed class SqliteObservationStore : IObservationStore
{
    private readonly string _rootDirectory;
    private readonly string _artifactsDirectory;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public int MaxCount { get; init; } = 500;
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromMinutes(5);

    public SqliteObservationStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        _artifactsDirectory = Path.Combine(_rootDirectory, "frames");
        Directory.CreateDirectory(_artifactsDirectory);

        var dbPath = Path.Combine(_rootDirectory, "observations.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS observations (
                id TEXT PRIMARY KEY,
                kind INTEGER NOT NULL,
                captured_at_utc TEXT NOT NULL,
                source_target TEXT NOT NULL,
                agent_key TEXT NULL,
                session_id TEXT NULL,
                artifact_path TEXT NOT NULL,
                byte_size INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_observations_captured_at ON observations(captured_at_utc DESC);
            """;
        command.ExecuteNonQuery();
    }

    public async Task<ObservationRecord> StoreAsync(ObservationWriteRequest request, CancellationToken ct = default)
    {
        var extension = NormalizeExtension(request.FileExtension);
        var artifactPath = Path.Combine(_artifactsDirectory, $"{request.Id}{extension}");

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_artifactsDirectory);
            await File.WriteAllBytesAsync(artifactPath, request.ArtifactBytes, ct).ConfigureAwait(false);

            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT OR REPLACE INTO observations
                (id, kind, captured_at_utc, source_target, agent_key, session_id, artifact_path, byte_size)
                VALUES ($id, $kind, $captured_at_utc, $source_target, $agent_key, $session_id, $artifact_path, $byte_size);
                """;
            insert.Parameters.AddWithValue("$id", request.Id);
            insert.Parameters.AddWithValue("$kind", (int)request.Kind);
            insert.Parameters.AddWithValue("$captured_at_utc", request.CapturedAtUtc.ToString("O"));
            insert.Parameters.AddWithValue("$source_target", request.SourceTarget);
            insert.Parameters.AddWithValue("$agent_key", (object?)request.AgentKey ?? DBNull.Value);
            insert.Parameters.AddWithValue("$session_id", (object?)request.SessionId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$artifact_path", artifactPath);
            insert.Parameters.AddWithValue("$byte_size", request.ArtifactBytes.LongLength);
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await TrimExpiredAsync(connection, transaction, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            return new ObservationRecord
            {
                Id = request.Id,
                Kind = request.Kind,
                CapturedAtUtc = request.CapturedAtUtc,
                SourceTarget = request.SourceTarget,
                AgentKey = request.AgentKey,
                SessionId = request.SessionId,
                ArtifactPath = artifactPath,
                ByteSize = request.ArtifactBytes.LongLength
            };
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<ObservationRecord>> GetRecentAsync(int count = 50, CancellationToken ct = default)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, kind, captured_at_utc, source_target, agent_key, session_id, artifact_path, byte_size
            FROM observations
            ORDER BY captured_at_utc DESC
            LIMIT $count;
            """;
        command.Parameters.AddWithValue("$count", count);

        var results = new List<ObservationRecord>();
        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new ObservationRecord
            {
                Id = reader.GetString(0),
                Kind = (ObservationKind)reader.GetInt32(1),
                CapturedAtUtc = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                SourceTarget = reader.GetString(3),
                AgentKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                SessionId = reader.IsDBNull(5) ? null : reader.GetString(5),
                ArtifactPath = reader.GetString(6),
                ByteSize = reader.GetInt64(7)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<ObservationRecord>> GetByTimeRangeAsync(
        string sessionId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, kind, captured_at_utc, source_target, agent_key, session_id, artifact_path, byte_size
            FROM observations
            WHERE session_id = $sessionId
              AND captured_at_utc >= $start
              AND captured_at_utc <= $end
            ORDER BY captured_at_utc ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$start", startUtc.ToString("O"));
        command.Parameters.AddWithValue("$end", endUtc.ToString("O"));

        var results = new List<ObservationRecord>();
        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new ObservationRecord
            {
                Id = reader.GetString(0),
                Kind = (ObservationKind)reader.GetInt32(1),
                CapturedAtUtc = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                SourceTarget = reader.GetString(3),
                AgentKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                SessionId = reader.IsDBNull(5) ? null : reader.GetString(5),
                ArtifactPath = reader.GetString(6),
                ByteSize = reader.GetInt64(7)
            });
        }

        return results;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private async Task TrimExpiredAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
    {
        var staleIds = new List<(string Id, string ArtifactPath)>();
        var cutoffUtc = DateTime.UtcNow - MaxAge;

        using (var staleCommand = connection.CreateCommand())
        {
            staleCommand.Transaction = transaction;
            staleCommand.CommandText =
                """
                SELECT id, artifact_path
                FROM observations
                WHERE captured_at_utc < $cutoff;
                """;
            staleCommand.Parameters.AddWithValue("$cutoff", cutoffUtc.ToString("O"));
            using var staleReader = await staleCommand.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await staleReader.ReadAsync(ct).ConfigureAwait(false))
                staleIds.Add((staleReader.GetString(0), staleReader.GetString(1)));
        }

        var overflow = new List<(string Id, string ArtifactPath)>();
        using (var overflowCommand = connection.CreateCommand())
        {
            overflowCommand.Transaction = transaction;
            overflowCommand.CommandText =
                """
                SELECT id, artifact_path
                FROM observations
                ORDER BY captured_at_utc DESC
                LIMIT -1 OFFSET $offset;
                """;
            overflowCommand.Parameters.AddWithValue("$offset", MaxCount);
            using var overflowReader = await overflowCommand.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await overflowReader.ReadAsync(ct).ConfigureAwait(false))
                overflow.Add((overflowReader.GetString(0), overflowReader.GetString(1)));
        }

        foreach (var item in staleIds.Concat(overflow).DistinctBy(x => x.Id))
        {
            using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM observations WHERE id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", item.Id);
            await deleteCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            TryDeleteArtifact(item.ArtifactPath);
        }
    }

    private static void TryDeleteArtifact(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; stale files should not break the capture pipeline.
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ".bin";

        return extension.StartsWith('.') ? extension : $".{extension}";
    }
}
