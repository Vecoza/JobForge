using JobForge.Core;

namespace JobForge.Core.Tests;

public class BackoffCalculatorTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    public void GetDelay_ReturnsExpectedDelayForAttemptCount(int attemptCount, int expectedMinutes)
    {
        var delay = BackoffCalculator.GetDelay(attemptCount);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), delay);
    }
}
