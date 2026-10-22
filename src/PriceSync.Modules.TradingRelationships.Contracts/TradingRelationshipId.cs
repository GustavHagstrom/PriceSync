namespace PriceSync.Modules.TradingRelationships.Contracts;

public readonly record struct TradingRelationshipId
{
    private TradingRelationshipId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("TradingRelationshipId cannot be empty.", nameof(value));
        }
        Value = value;
    }
    public Guid Value { get; }
    public override string ToString() => Value.ToString();
    public static TradingRelationshipId Create() => new TradingRelationshipId(Guid.CreateVersion7());
    public static TradingRelationshipId FromGuid(Guid value) => new TradingRelationshipId(value);

}