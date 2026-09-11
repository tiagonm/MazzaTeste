namespace Mazza.Orders.UnitTests.TestSupport;

/// <summary>
/// A <see cref="TimeProvider"/> frozen at a known instant.
///
/// Hand-rolled rather than pulled in from Microsoft.Extensions.TimeProvider.Testing:
/// these tests only need "what time is it", and one overridden method is cheaper than
/// a dependency. If a test ever needs to advance the clock or drive a timer,
/// <c>FakeTimeProvider</c> from that package is the right tool and this goes away.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    public static readonly DateTimeOffset DefaultNow =
        new(2026, 3, 14, 9, 26, 53, TimeSpan.Zero);

    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset? now = null) => _now = now ?? DefaultNow;

    public override DateTimeOffset GetUtcNow() => _now;
}
