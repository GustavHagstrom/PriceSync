namespace PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;


public sealed class FrozenTimeProvider : TimeProvider
{
    public FrozenTimeProvider(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }
    public DateTimeOffset UtcNow { get; set; }
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

