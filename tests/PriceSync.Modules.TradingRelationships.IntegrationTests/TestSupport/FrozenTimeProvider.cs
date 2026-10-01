namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

public sealed partial class CreateApiTests
{
    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
