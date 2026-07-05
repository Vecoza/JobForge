namespace JobForge.Core;

public static class BackoffCalculator
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];

    public static TimeSpan GetDelay(int attemptCount) => Delays[attemptCount - 1];
}
