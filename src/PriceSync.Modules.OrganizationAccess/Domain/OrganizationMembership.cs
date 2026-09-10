using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.OrganizationAccess.Domain;

internal sealed class OrganizationMembership
{
    private readonly HashSet<OrganizationRole> _roles;

    private OrganizationMembership(
        OrganizationMembershipId id,
        OrganizationId organizationId,
        PersonId personId,
        HashSet<OrganizationRole> roles,
        DateTimeOffset startedAt)
    {
        Id = id;
        OrganizationId = organizationId;
        PersonId = personId;
        _roles = roles;
        StartedAt = startedAt;
        Status = OrganizationMembershipStatus.Active;
    }

    public OrganizationMembershipId Id { get; }

    public OrganizationId OrganizationId { get; }

    public PersonId PersonId { get; }

    public OrganizationMembershipStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? EndedAt { get; private set; }

    public IReadOnlyCollection<OrganizationRole> Roles => _roles.ToArray();

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

        return new OrganizationMembership(id, organizationId, personId, explicitRoles, timeProvider.GetUtcNow());
    }

    public bool HasAuthority(OrganizationRole role)
    {
        return _roles.Contains(role)
            || role == OrganizationRole.Member && _roles.Contains(OrganizationRole.Owner);
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
