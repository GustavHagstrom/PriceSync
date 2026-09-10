namespace PriceSync.Modules.OrganizationAccess.Domain;

internal readonly record struct OrganizationMembershipId
{
    public OrganizationMembershipId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("OrganizationMembershipId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();
}
