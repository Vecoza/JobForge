using JobForge.Core;
using JobForge.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace JobForge.Worker.Tests;

public class ClaimPendingJobsAsyncTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task ClaimPendingJobsAsync_ConcurrentCalls_ClaimEveryJobExactlyOnce()
    {
        const int jobCount = 30;

        await using (var seedDb = CreateDbContext())
        {
            var jobs = Enumerable.Range(0, jobCount).Select(_ => new Job
            {
                RequestId = Guid.NewGuid(),
                RecipientEmail = "test@example.com",
                Subject = "Test",
                Body = "Test body",
                Status = JobStatus.Pending,
                NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            });

            seedDb.Jobs.AddRange(jobs);
            await seedDb.SaveChangesAsync();
        }

        await using var db1 = CreateDbContext();
        await using var db2 = CreateDbContext();

        var claim1Task = db1.ClaimPendingJobsAsync(jobCount, CancellationToken.None);
        var claim2Task = db2.ClaimPendingJobsAsync(jobCount, CancellationToken.None);

        var results = await Task.WhenAll(claim1Task, claim2Task);

        var claimed1Ids = results[0].Select(j => j.Id).ToList();
        var claimed2Ids = results[1].Select(j => j.Id).ToList();
        var allClaimedIds = claimed1Ids.Concat(claimed2Ids).ToList();

        Assert.Equal(jobCount, allClaimedIds.Count);
        Assert.Equal(allClaimedIds.Count, allClaimedIds.Distinct().Count());
        Assert.Empty(claimed1Ids.Intersect(claimed2Ids));
    }
}
