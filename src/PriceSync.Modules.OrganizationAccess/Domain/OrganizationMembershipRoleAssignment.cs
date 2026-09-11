namespace PriceSync.Modules.OrganizationAccess.Domain;

internal sealed class OrganizationMembershipRoleAssignment
{
    private OrganizationMembershipRoleAssignment()
    {
    }

    public OrganizationMembershipRoleAssignment(OrganizationRole role)
    {
        Role = role;
    }

    public OrganizationRole Role { get; private set; }

    public override bool Equals(object? obj)
    {
        return obj is OrganizationMembershipRoleAssignment assignment && assignment.Role == Role;
    }

    public override int GetHashCode() => Role.GetHashCode();
}
