namespace JobForge.Core.Entities;

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
