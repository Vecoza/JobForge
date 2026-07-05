namespace JobForge.Core.Entities;

public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RequestId { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public int AttemptCount { get; set; } = 0;
    public int MaxAttempts { get; set; } = 4;

    public DateTimeOffset NextRunAt { get; set; } = DateTimeOffset.UtcNow;

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
