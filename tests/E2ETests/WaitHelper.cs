namespace E2ETests;

internal static class WaitHelper
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Polls <paramref name="condition"/> until it returns true or <paramref name="timeout"/> elapses.
    /// Throws <see cref="TimeoutException"/> if the condition is never satisfied.
    /// </summary>
    public static async Task WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan? interval = null,
        string? description = null)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var checkInterval = interval ?? DefaultInterval;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
                return;

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(checkInterval < remaining ? checkInterval : remaining);
        }

        throw new TimeoutException(
            $"Condition not met within {timeout}: {description ?? "(no description)"}");
    }
}
