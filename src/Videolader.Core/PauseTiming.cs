namespace Videolader.Core;

/// <summary>Works out how long to wait between two downloads.</summary>
public static class PauseTiming
{
    /// <summary>A random pause lasts between the set value and this factor times the set value.</summary>
    public const double RandomMaxFactor = 1.5;

    /// <summary>
    /// The next pause. The set value is the minimum; with <paramref name="random"/> a random amount of up to
    /// 50 % is added, so the downloads do not start at an even rhythm.
    /// </summary>
    public static TimeSpan Next(int seconds, bool random, Random? rng = null)
    {
        if (seconds <= 0)
            return TimeSpan.Zero;
        if (!random)
            return TimeSpan.FromSeconds(seconds);

        var factor = 1 + (RandomMaxFactor - 1) * (rng ?? Random.Shared).NextDouble();
        return TimeSpan.FromSeconds(seconds * factor);
    }
}
