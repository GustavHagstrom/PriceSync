using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.UnitTests.Domain;

public sealed class OrganizationMembershipTests
{
    [Fact]
    public void Create_establishes_active_membership_with_explicit_roles_and_start_timestamp()
    {
        var id = NewMembershipId();
        var organizationId = NewOrganizationId();
        var personId = NewPersonId();
        var startedAt = new DateTimeOffset(2026, 9, 10, 12, 30, 0, TimeSpan.Zero);

        var membership = OrganizationMembership.Create(
            id,
            organizationId,
            personId,
            [OrganizationRole.Member],
            new FrozenTimeProvider(startedAt));

        membership.Id.Should().Be(id);
        membership.OrganizationId.Should().Be(organizationId);
        membership.PersonId.Should().Be(personId);
        membership.Status.Should().Be(OrganizationMembershipStatus.Active);
        membership.StartedAt.Should().Be(startedAt);
        membership.EndedAt.Should().BeNull();
        membership.Roles.Should().BeEquivalentTo([OrganizationRole.Member]);
    }

    [Fact]
    public void OrganizationMembershipId_rejects_empty_guid()
    {
        var create = () => new OrganizationMembershipId(Guid.Empty);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_null_roles()
    {
        var create = () => OrganizationMembership.Create(
            NewMembershipId(),
            NewOrganizationId(),
            NewPersonId(),
            null!,
            NewFrozenTimeProvider());

        create.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_rejects_empty_explicit_roles()
    {
        var create = () => OrganizationMembership.Create(
            NewMembershipId(),
            NewOrganizationId(),
            NewPersonId(),
            [],
            NewFrozenTimeProvider());

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Owner_authority_includes_member_without_returning_member_as_explicit_role()
    {
        var membership = OrganizationMembership.Create(
            NewMembershipId(),
            NewOrganizationId(),
            NewPersonId(),
            [OrganizationRole.Owner],
            NewFrozenTimeProvider());

        membership.HasAuthority(OrganizationRole.Owner).Should().BeTrue();
        membership.HasAuthority(OrganizationRole.Member).Should().BeTrue();
        membership.Roles.Should().BeEquivalentTo([OrganizationRole.Owner]);
    }

    [Fact]
    public void Member_authority_does_not_include_owner()
    {
        var membership = OrganizationMembership.Create(
            NewMembershipId(),
            NewOrganizationId(),
            NewPersonId(),
            [OrganizationRole.Member],
            NewFrozenTimeProvider());

        membership.HasAuthority(OrganizationRole.Member).Should().BeTrue();
        membership.HasAuthority(OrganizationRole.Owner).Should().BeFalse();
    }

    [Fact]
    public void Create_supports_multiple_explicit_roles()
    {
        var membership = OrganizationMembership.Create(
            NewMembershipId(),
            NewOrganizationId(),
            NewPersonId(),
            [OrganizationRole.Owner, OrganizationRole.Member],
            NewFrozenTimeProvider());

        membership.Roles.Should().BeEquivalentTo([OrganizationRole.Owner, OrganizationRole.Member]);
    }

    [Fact]
    public void Create_deduplicates_explicit_roles()
    {
        var membership = OrganizationMembership.Create(
            NewMembershipId(),
            NewOrganizationId(),
            NewPersonId(),
            [OrganizationRole.Member, OrganizationRole.Member],
            NewFrozenTimeProvider());

        membership.Roles.Should().BeEquivalentTo([OrganizationRole.Member]);
    }

    [Fact]
    public void Roles_returns_snapshot_that_cannot_mutate_membership_roles()
    {
        var membership = OrganizationMembership.Create(
            NewMembershipId(),
            NewOrganizationId(),
            NewPersonId(),
            [OrganizationRole.Member],
            NewFrozenTimeProvider());

        var returnedRoles = membership.Roles.Should().BeAssignableTo<OrganizationRole[]>().Subject;
        returnedRoles[0] = OrganizationRole.Owner;

        membership.Roles.Should().BeEquivalentTo([OrganizationRole.Member]);
        membership.HasAuthority(OrganizationRole.Owner).Should().BeFalse();
    }

    [Fact]
    public void End_moves_active_membership_to_ended_with_current_utc_timestamp()
    {
        var membership = NewMembership();
        var endedAt = new DateTimeOffset(2026, 9, 10, 14, 45, 0, TimeSpan.Zero);

        membership.End(new FrozenTimeProvider(endedAt));

        membership.Status.Should().Be(OrganizationMembershipStatus.Ended);
        membership.EndedAt.Should().Be(endedAt);
    }

    [Fact]
    public void End_rejects_null_time_provider()
    {
        var membership = NewMembership();

        var end = () => membership.End(null!);

        end.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ended_is_terminal_for_organization_membership()
    {
        var membership = NewMembership();
        var endedAt = new DateTimeOffset(2026, 9, 10, 14, 45, 0, TimeSpan.Zero);
        membership.End(new FrozenTimeProvider(endedAt));

        var endAgain = () => membership.End(new FrozenTimeProvider(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero)));

        endAgain.Should().Throw<InvalidOperationException>();
        membership.Status.Should().Be(OrganizationMembershipStatus.Ended);
        membership.EndedAt.Should().Be(endedAt);
    }

    [Fact]
    public void Organization_membership_is_not_a_child_collection_on_organization()
    {
        typeof(Organization)
            .GetProperties()
            .Select(property => property.PropertyType)
            .Should()
            .NotContain(type => type == typeof(OrganizationMembership)
                || type.IsGenericType
                && type.GetGenericArguments().Contains(typeof(OrganizationMembership)));
    }

    private static OrganizationMembershipId NewMembershipId() => new(Guid.NewGuid());

    private static OrganizationId NewOrganizationId() => new(Guid.NewGuid());

    private static PersonId NewPersonId() => new(Guid.NewGuid());

    private static FrozenTimeProvider NewFrozenTimeProvider() => new(new DateTimeOffset(2026, 9, 10, 12, 30, 0, TimeSpan.Zero));

    private static OrganizationMembership NewMembership() => OrganizationMembership.Create(
        NewMembershipId(),
        NewOrganizationId(),
        NewPersonId(),
        [OrganizationRole.Member],
        NewFrozenTimeProvider());

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
