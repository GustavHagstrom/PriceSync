namespace PriceSync.Modules.Agreements.Contracts;

public readonly record struct AgreementVersionId
{
    public AgreementVersionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("AgreementVersionId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();

    public static AgreementVersionId Create() => new AgreementVersionId(Guid.CreateVersion7());
}
