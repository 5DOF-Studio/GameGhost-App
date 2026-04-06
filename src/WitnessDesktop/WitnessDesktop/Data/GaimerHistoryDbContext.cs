using Microsoft.EntityFrameworkCore;
using WitnessDesktop.Data.Entities;

namespace WitnessDesktop.Data;

/// <summary>
/// EF Core DbContext for the Gaimer history database.
/// Stores sessions, chat messages, timeline checkpoints, and timeline events.
/// </summary>
public class GaimerHistoryDbContext : DbContext
{
    public DbSet<SessionRecord> Sessions { get; set; } = null!;
    public DbSet<ChatMessageRecord> ChatMessages { get; set; } = null!;
    public DbSet<TimelineCheckpointRecord> TimelineCheckpoints { get; set; } = null!;
    public DbSet<TimelineEventRecord> TimelineEvents { get; set; } = null!;

    public GaimerHistoryDbContext(DbContextOptions<GaimerHistoryDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Creates a DbContext configured for a specific SQLite file path.
    /// Configures WAL journal mode and a 5-second busy timeout to handle
    /// concurrent fire-and-forget writes without "database is locked" errors.
    /// Useful for tests and bootstrap scenarios.
    /// </summary>
    public static GaimerHistoryDbContext CreateForPath(string dbPath)
    {
        var options = new DbContextOptionsBuilder<GaimerHistoryDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var ctx = new GaimerHistoryDbContext(options);

        // WAL mode allows concurrent readers + single writer without locking.
        // busy_timeout tells SQLite to retry for up to 5s before returning SQLITE_BUSY.
        // These pragmas are connection-scoped, so we set them every time a context is created.
        try
        {
            var conn = ctx.Database.GetDbConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Swallow — context may be used against a non-existent file for EnsureCreated.
            // The pragmas will be applied on next CreateForPath after the DB exists.
        }

        return ctx;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SessionRecord
        modelBuilder.Entity<SessionRecord>(entity =>
        {
            entity.HasKey(e => e.SessionId);

            entity.HasMany(e => e.ChatMessages)
                .WithOne(e => e.Session)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Checkpoints)
                .WithOne(e => e.Session)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatMessageRecord — composite index for session-ordered queries
        modelBuilder.Entity<ChatMessageRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionId, e.TimestampUtc });
        });

        // TimelineCheckpointRecord — composite index for session-ordered queries
        modelBuilder.Entity<TimelineCheckpointRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionId, e.CreatedAtUtc });

            entity.HasMany(e => e.Events)
                .WithOne(e => e.Checkpoint)
                .HasForeignKey(e => e.CheckpointId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TimelineEventRecord — composite index for session-ordered queries
        modelBuilder.Entity<TimelineEventRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionId, e.CreatedAtUtc });

            // FK to Session (cascade via Session → Events path)
            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
