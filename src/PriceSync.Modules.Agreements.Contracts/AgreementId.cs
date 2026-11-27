namespace PriceSync.Modules.Agreements.Contracts;

public readonly record struct AgreementId
{
    public AgreementId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("AgreementId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();

    public static AgreementId Create() => new AgreementId(Guid.CreateVersion7());
}
