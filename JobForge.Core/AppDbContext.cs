using JobForge.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobForge.Core;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<FailedJob> FailedJobs => Set<FailedJob>();

    public Task<List<Job>> ClaimPendingJobsAsync(int batchSize, CancellationToken cancellationToken) =>
        Jobs.FromSqlInterpolated(
                $"""
                UPDATE "Jobs"
                SET "Status" = 'Processing', "ClaimedAt" = now(), "UpdatedAt" = now()
                WHERE "Id" IN (
                    SELECT "Id" FROM "Jobs"
                    WHERE "Status" = 'Pending' AND "NextRunAt" <= now()
                    ORDER BY "NextRunAt"
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *;
                """)
            .ToListAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(j => j.Id);

            entity.HasIndex(j => j.RequestId).IsUnique();

            entity.Property(j => j.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(j => j.RecipientEmail).HasMaxLength(320);
            entity.Property(j => j.Subject).HasMaxLength(500);

            entity.HasIndex(j => new { j.Status, j.NextRunAt });
        });

        modelBuilder.Entity<FailedJob>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => f.OriginalJobId);
            entity.Property(f => f.RecipientEmail).HasMaxLength(320);
            entity.Property(f => f.Subject).HasMaxLength(500);
        });
    }
}
