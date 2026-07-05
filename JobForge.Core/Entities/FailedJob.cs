namespace JobForge.Core.Entities;

// Dead-letter record. Inserted when a Job exhausts MaxAttempts (or hits a
// permanent failure) — the original Job row is kept as-is (Status=Failed)
// for audit history; this table is a fast lookup/alerting surface.
public class FailedJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OriginalJobId { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string FinalError { get; set; } = string.Empty;
    public int AttemptCount { get; set; }

    public DateTimeOffset FailedAt { get; set; } = DateTimeOffset.UtcNow;
}
