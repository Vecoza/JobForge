namespace JobForge.Core;

public static class RetryPolicy
{
    public static bool ShouldRetry(int attemptCount, int maxAttempts) => attemptCount < maxAttempts;
}
