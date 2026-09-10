namespace PriceSync.Modules.OrganizationAccess.Contracts;

public readonly record struct PersonId
{
    public PersonId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("PersonId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();
}
