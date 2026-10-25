using Videolader.Core;

namespace Videolader.Tests;

public class PauseTimingTests
{
    [Fact]
    public void Fixed_pause_is_the_set_value()
    {
        Assert.Equal(TimeSpan.FromSeconds(45), PauseTiming.Next(45, random: false));
    }

    [Fact]
    public void Zero_stays_zero_even_when_random()
    {
        Assert.Equal(TimeSpan.Zero, PauseTiming.Next(0, random: true));
    }

    [Fact]
    public void Random_pause_is_never_shorter_than_set_and_at_most_half_longer()
    {
        var rng = new Random(42);
        var pauses = Enumerable.Range(0, 1000).Select(_ => PauseTiming.Next(45, random: true, rng).TotalSeconds).ToList();

        Assert.All(pauses, p => Assert.InRange(p, 45, 67.5));
        Assert.True(pauses.Distinct().Count() > 900);
        Assert.True(pauses.Min() < 47 && pauses.Max() > 65);
    }

    [Fact]
    public void Random_pause_is_on_by_default()
    {
        Assert.True(new AppSettings().RandomPause);
    }
}
