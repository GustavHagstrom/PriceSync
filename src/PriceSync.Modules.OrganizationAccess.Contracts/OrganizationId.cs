namespace PriceSync.Modules.OrganizationAccess.Contracts;

public readonly record struct OrganizationId
{
    public OrganizationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();

    public static OrganizationId Create() => new OrganizationId(Guid.CreateVersion7());
}
