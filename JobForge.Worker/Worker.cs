using JobForge.Core;
using JobForge.Core.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace JobForge.Worker;

public class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<MailtrapOptions> mailtrapOptions,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private const string FromAddress = "noreply@jobforge.local";

    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            await ProcessBatchAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var claimedJobs = await db.Jobs
            .FromSqlRaw(
                """
                UPDATE "Jobs"
                SET "Status" = 'Processing', "ClaimedAt" = now(), "UpdatedAt" = now()
                WHERE "Id" IN (
                    SELECT "Id" FROM "Jobs"
                    WHERE "Status" = 'Pending' AND "NextRunAt" <= now()
                    ORDER BY "NextRunAt"
                    LIMIT 10
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *;
                """)
            .ToListAsync(stoppingToken);

        logger.LogInformation("Claimed {Count} job(s) for processing", claimedJobs.Count);

        foreach (var job in claimedJobs)
        {
            await ProcessJobAsync(db, job, CancellationToken.None);
        }
    }

    private async Task ProcessJobAsync(AppDbContext db, Job job, CancellationToken cancellationToken)
    {
        try
        {
            await SendEmailAsync(job, cancellationToken);

            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            logger.LogInformation("Job {JobId} completed successfully", job.Id);
        }
        catch (Exception ex)
        {
            job.AttemptCount++;
            job.LastError = ex.Message;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            if (job.AttemptCount < job.MaxAttempts)
            {
                job.Status = JobStatus.Pending;
                job.NextRunAt = DateTimeOffset.UtcNow + BackoffDelays[job.AttemptCount - 1];

                logger.LogWarning(
                    "Job {JobId} failed on attempt {AttemptCount}, retrying at {NextRunAt}",
                    job.Id, job.AttemptCount, job.NextRunAt);
            }
            else
            {
                job.Status = JobStatus.Failed;

                db.FailedJobs.Add(new FailedJob
                {
                    OriginalJobId = job.Id,
                    RecipientEmail = job.RecipientEmail,
                    Subject = job.Subject,
                    FinalError = ex.Message,
                    AttemptCount = job.AttemptCount,
                    FailedAt = DateTimeOffset.UtcNow
                });

                logger.LogError(
                    "Job {JobId} dead-lettered after {AttemptCount} attempts",
                    job.Id, job.AttemptCount);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SendEmailAsync(Job job, CancellationToken cancellationToken)
    {
        var options = mailtrapOptions.Value;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(FromAddress));
        message.To.Add(MailboxAddress.Parse(job.RecipientEmail));
        message.Subject = job.Subject;
        message.Body = new TextPart("plain") { Text = job.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, SecureSocketOptions.Auto, cancellationToken);
        await client.AuthenticateAsync(options.Username, options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
