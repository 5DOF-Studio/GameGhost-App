using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Replay;

public sealed class SqliteSegmentAnalysisStore : ISegmentAnalysisStore, IDisposable
{
    private readonly string _connectionString;
    private bool _initialized;
    private readonly object _initLock = new();

    public SqliteSegmentAnalysisStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={dbPath}";
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        EnsureSchema(conn);
        return conn;
    }

    private void EnsureSchema(SqliteConnection conn)
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS SegmentAnalyses (
                    Id TEXT PRIMARY KEY,
                    SessionId TEXT,
                    StartUtc TEXT NOT NULL,
                    EndUtc TEXT NOT NULL,
                    RawJson TEXT NOT NULL,
                    BeatsJson TEXT NOT NULL,
                    NarrativeSummary TEXT NOT NULL,
                    SearchableText TEXT NOT NULL,
                    BeatCount INTEGER NOT NULL,
                    PackId TEXT,
                    Model TEXT,
                    CreatedAt TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_SegmentAnalyses_Time
                    ON SegmentAnalyses (StartUtc, EndUtc);

                CREATE INDEX IF NOT EXISTS IX_SegmentAnalyses_Session
                    ON SegmentAnalyses (SessionId);

                CREATE VIRTUAL TABLE IF NOT EXISTS SegmentAnalyses_fts
                    USING fts5(Id UNINDEXED, NarrativeSummary, SearchableText);
                """;
            cmd.ExecuteNonQuery();
            _initialized = true;
        }
    }

    public async Task IngestAsync(VideoAnalysisResult result, CancellationToken ct = default)
    {
        var searchableText = string.Join(" | ", result.Beats.Select(b => b.Assessment));
        var beatsJson = JsonSerializer.Serialize(result.Beats);
        var now = DateTimeOffset.UtcNow.ToString("O");

        await using var conn = OpenConnection();

        // [C2] Wrap upsert + FTS sync in a transaction for atomicity
        await using var txn = await conn.BeginTransactionAsync(ct);

        // Upsert main table (with SessionId [C3])
        await using var upsert = conn.CreateCommand();
        upsert.Transaction = (SqliteTransaction)txn;
        upsert.CommandText = """
            INSERT INTO SegmentAnalyses (Id, SessionId, StartUtc, EndUtc, RawJson, BeatsJson, NarrativeSummary, SearchableText, BeatCount, PackId, Model, CreatedAt)
            VALUES ($id, $sessionId, $startUtc, $endUtc, $rawJson, $beatsJson, $summary, $searchable, $beatCount, $packId, $model, $createdAt)
            ON CONFLICT(Id) DO UPDATE SET
                SessionId = excluded.SessionId,
                RawJson = excluded.RawJson,
                BeatsJson = excluded.BeatsJson,
                NarrativeSummary = excluded.NarrativeSummary,
                SearchableText = excluded.SearchableText,
                BeatCount = excluded.BeatCount,
                CreatedAt = excluded.CreatedAt;
            """;
        upsert.Parameters.AddWithValue("$id", result.SegmentId);
        upsert.Parameters.AddWithValue("$sessionId", (object?)result.SessionId ?? DBNull.Value);
        upsert.Parameters.AddWithValue("$startUtc", result.StartUtc.ToString("O"));
        upsert.Parameters.AddWithValue("$endUtc", result.EndUtc.ToString("O"));
        upsert.Parameters.AddWithValue("$rawJson", result.RawJson);
        upsert.Parameters.AddWithValue("$beatsJson", beatsJson);
        upsert.Parameters.AddWithValue("$summary", result.NarrativeSummary);
        upsert.Parameters.AddWithValue("$searchable", searchableText);
        upsert.Parameters.AddWithValue("$beatCount", result.Beats.Count);
        upsert.Parameters.AddWithValue("$packId", (object?)result.PackId ?? DBNull.Value);
        upsert.Parameters.AddWithValue("$model", (object?)result.Model ?? DBNull.Value);
        upsert.Parameters.AddWithValue("$createdAt", now);
        await upsert.ExecuteNonQueryAsync(ct);

        // Sync FTS5 — delete old entry (if any), then insert fresh
        await using var ftsDelete = conn.CreateCommand();
        ftsDelete.Transaction = (SqliteTransaction)txn;
        ftsDelete.CommandText = "DELETE FROM SegmentAnalyses_fts WHERE Id = $id;";
        ftsDelete.Parameters.AddWithValue("$id", result.SegmentId);
        await ftsDelete.ExecuteNonQueryAsync(ct);

        await using var ftsInsert = conn.CreateCommand();
        ftsInsert.Transaction = (SqliteTransaction)txn;
        ftsInsert.CommandText = "INSERT INTO SegmentAnalyses_fts (Id, NarrativeSummary, SearchableText) VALUES ($id, $summary, $searchable);";
        ftsInsert.Parameters.AddWithValue("$id", result.SegmentId);
        ftsInsert.Parameters.AddWithValue("$summary", result.NarrativeSummary);
        ftsInsert.Parameters.AddWithValue("$searchable", searchableText);
        await ftsInsert.ExecuteNonQueryAsync(ct);

        await txn.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AnalyzedBeat>> SearchAsync(string query, DateTimeOffset? startUtc = null, DateTimeOffset? endUtc = null, CancellationToken ct = default)
    {
        await using var conn = OpenConnection();
        await using var cmd = conn.CreateCommand();

        var sql = """
            SELECT sa.SearchableText, sa.StartUtc, sa.EndUtc, sa.BeatsJson
            FROM SegmentAnalyses_fts fts
            JOIN SegmentAnalyses sa ON sa.Id = fts.Id
            WHERE SegmentAnalyses_fts MATCH $query
            """;

        if (startUtc.HasValue)
            sql += " AND sa.StartUtc >= $startUtc";
        if (endUtc.HasValue)
            sql += " AND sa.EndUtc <= $endUtc";

        sql += " ORDER BY rank LIMIT 50;";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$query", EscapeFts5Query(query));
        if (startUtc.HasValue) cmd.Parameters.AddWithValue("$startUtc", startUtc.Value.ToString("O"));
        if (endUtc.HasValue) cmd.Parameters.AddWithValue("$endUtc", endUtc.Value.ToString("O"));

        var beats = new List<AnalyzedBeat>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var beatsJson = reader.GetString(3);
            try
            {
                var parsed = JsonSerializer.Deserialize<List<AnalyzedBeat>>(beatsJson);
                if (parsed != null) beats.AddRange(parsed);
            }
            catch
            {
                // Fallback: synthesize a beat from the searchable text
                var segStart = reader.GetString(1);
                var segEnd = reader.GetString(2);
                beats.Add(new AnalyzedBeat
                {
                    StartTime = segStart,
                    EndTime = segEnd,
                    Assessment = reader.GetString(0) // SearchableText as fallback
                });
            }
        }
        return beats;
    }

    public async Task<string?> GetSummaryAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default)
    {
        await using var conn = OpenConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT NarrativeSummary FROM SegmentAnalyses
            WHERE StartUtc >= $startUtc AND EndUtc <= $endUtc
            ORDER BY StartUtc;
            """;
        cmd.Parameters.AddWithValue("$startUtc", startUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$endUtc", endUtc.ToString("O"));

        var summaries = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            summaries.Add(reader.GetString(0));

        return summaries.Count > 0 ? string.Join(" ", summaries) : null;
    }

    public async Task<IReadOnlyList<VideoAnalysisResult>> GetByTimeRangeAsync(string sessionId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default)
    {
        await using var conn = OpenConnection();
        await using var cmd = conn.CreateCommand();
        // [C3] Filter by SessionId for proper session isolation
        cmd.CommandText = """
            SELECT Id, SessionId, StartUtc, EndUtc, RawJson, NarrativeSummary, BeatsJson, PackId, Model
            FROM SegmentAnalyses
            WHERE SessionId = $sessionId AND StartUtc >= $startUtc AND StartUtc <= $endUtc
            ORDER BY StartUtc;
            """;
        cmd.Parameters.AddWithValue("$sessionId", sessionId);
        cmd.Parameters.AddWithValue("$startUtc", startUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$endUtc", endUtc.ToString("O"));

        var results = new List<VideoAnalysisResult>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var rawJson = reader.GetString(4);
            var beatsJson = reader.GetString(6);
            var beats = TryParseBeats(beatsJson);
            results.Add(new VideoAnalysisResult
            {
                SegmentId = reader.GetString(0),
                SessionId = reader.IsDBNull(1) ? null : reader.GetString(1),
                StartUtc = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                EndUtc = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                RawJson = rawJson,
                NarrativeSummary = reader.GetString(5),
                Beats = beats,
                PackId = reader.IsDBNull(7) ? null : reader.GetString(7),
                Model = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }
        return results;
    }

    private static IReadOnlyList<AnalyzedBeat> TryParseBeats(string beatsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<AnalyzedBeat>>(beatsJson) ?? [];
        }
        catch { return []; }
    }

    private static string EscapeFts5Query(string query)
    {
        // Wrap each word in quotes to avoid FTS5 syntax errors from user input
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" OR ", words.Select(w => $"\"{w.Replace("\"", "")}\""));
    }

    public void Dispose() { /* Connection-per-call, nothing to dispose */ }
}
