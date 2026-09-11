using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.OrganizationAccess.Domain;

internal sealed class OrganizationMembership
{
    private readonly HashSet<OrganizationMembershipRoleAssignment> _roles;

    private OrganizationMembership(
        OrganizationMembershipId id,
        OrganizationId organizationId,
        PersonId personId,
        HashSet<OrganizationMembershipRoleAssignment> roles,
        DateTimeOffset startedAt)
    {
        Id = id;
        OrganizationId = organizationId;
        PersonId = personId;
        _roles = roles;
        StartedAt = startedAt;
        Status = OrganizationMembershipStatus.Active;
    }

    private OrganizationMembership()
    {
        _roles = [];
    }

    public OrganizationMembershipId Id { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public PersonId PersonId { get; private set; }

    public OrganizationMembershipStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public IReadOnlyCollection<OrganizationRole> Roles => _roles.Select(assignment => assignment.Role).ToArray();

    public static OrganizationMembership Create(
        OrganizationMembershipId id,
        OrganizationId organizationId,
        PersonId personId,
        IEnumerable<OrganizationRole> roles,
        TimeProvider timeProvider)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("OrganizationMembershipId cannot be empty.", nameof(id));
        }

        if (organizationId.Value == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (personId.Value == Guid.Empty)
        {
            throw new ArgumentException("PersonId cannot be empty.", nameof(personId));
        }

        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var explicitRoles = roles.ToHashSet();
        if (explicitRoles.Count == 0)
        {
            throw new ArgumentException("Active Organization Membership must have at least one role.", nameof(roles));
        }

        if (explicitRoles.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentException("Organization Membership roles must be defined Organization Roles.", nameof(roles));
        }

        return new OrganizationMembership(
            id,
            organizationId,
            personId,
            explicitRoles.Select(role => new OrganizationMembershipRoleAssignment(role)).ToHashSet(),
            timeProvider.GetUtcNow());
    }

    public bool HasAuthority(OrganizationRole role)
    {
        return _roles.Any(assignment => assignment.Role == role)
            || role == OrganizationRole.Member && _roles.Any(assignment => assignment.Role == OrganizationRole.Owner);
    }

    public void End(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == OrganizationMembershipStatus.Ended)
        {
            throw new InvalidOperationException("Ended Organization Membership cannot be ended again.");
        }

        Status = OrganizationMembershipStatus.Ended;
        EndedAt = timeProvider.GetUtcNow();
    }
}
