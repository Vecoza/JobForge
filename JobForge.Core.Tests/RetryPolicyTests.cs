using JobForge.Core;

namespace JobForge.Core.Tests;

public class RetryPolicyTests
{
    [Theory]
    [InlineData(1, 4, true)]
    [InlineData(2, 4, true)]
    [InlineData(3, 4, true)]
    [InlineData(4, 4, false)]
    [InlineData(5, 4, false)]
    public void ShouldRetry_ReturnsExpectedDecision(int attemptCount, int maxAttempts, bool expected)
    {
        var shouldRetry = RetryPolicy.ShouldRetry(attemptCount, maxAttempts);

        Assert.Equal(expected, shouldRetry);
    }
}
