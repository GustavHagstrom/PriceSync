namespace PriceSync.Modules.TradingRelationships.Contracts;

public readonly record struct TradingRelationshipId
{
    public TradingRelationshipId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("TradingRelationshipId cannot be empty.", nameof(value));
        }
        Value = value;
    }
    public Guid Value { get; }
    public override string ToString() => Value.ToString();

}