namespace PriceSync.Modules.PricePublication.Contracts;

public readonly record struct PriceListId
{
    public PriceListId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("PriceListId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();

    public static PriceListId Create() => new PriceListId(Guid.CreateVersion7());
}
