using JobForge.Api.Dtos;
using JobForge.Core;
using JobForge.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobForge.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsResponse>> GetStats(CancellationToken cancellationToken)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, TimeSpan.Zero);
        var tomorrowStart = todayStart.AddDays(1);

        var jobStats = await db.Jobs
            .GroupBy(_ => 1)
            .Select(g => new
            {
                PendingCount = g.Count(j => j.Status == JobStatus.Pending),
                ProcessingCount = g.Count(j => j.Status == JobStatus.Processing),
                ProcessedToday = g.Count(j =>
                    j.Status == JobStatus.Completed &&
                    j.CompletedAt >= todayStart &&
                    j.CompletedAt < tomorrowStart)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var failedCount = await db.FailedJobs.CountAsync(cancellationToken);

        return Ok(new DashboardStatsResponse(
            jobStats?.PendingCount ?? 0,
            jobStats?.ProcessingCount ?? 0,
            jobStats?.ProcessedToday ?? 0,
            failedCount));
    }
}
