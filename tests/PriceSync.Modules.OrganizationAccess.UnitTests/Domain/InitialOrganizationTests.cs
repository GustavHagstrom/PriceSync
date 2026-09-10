using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.UnitTests.Domain;

public sealed class InitialOrganizationTests
{
    [Fact]
    public void Create_produces_active_organization_and_separate_active_owner_membership_for_current_person()
    {
        var organizationId = new OrganizationId(Guid.Parse("01994aca-a1c3-7d23-9803-2d3e8fb705a1"));
        var membershipId = new OrganizationMembershipId(Guid.Parse("01994aca-fb8c-793a-bfa6-76073a02c68a"));
        var personId = new PersonId(Guid.Parse("01994acb-23f5-72dc-bda0-a0bc06bd2ef2"));
        var legalIdentity = LegalOrganizationIdentity.Create("SE", "5560160680");
        var now = new DateTimeOffset(2026, 9, 10, 12, 30, 0, TimeSpan.Zero);

        var initialState = InitialOrganizationState.Create(
            organizationId,
            membershipId,
            "  Acme AB  ",
            legalIdentity,
            new StubCurrentPerson(personId),
            new FrozenTimeProvider(now));

        initialState.Organization.Id.Should().Be(organizationId);
        initialState.Organization.DisplayName.Should().Be("Acme AB");
        initialState.Organization.LegalIdentity.Should().Be(legalIdentity);
        initialState.Organization.Status.Should().Be(OrganizationStatus.Active);
        initialState.Organization.CreatedAt.Should().Be(now);

        initialState.FirstOwnerMembership.Id.Should().Be(membershipId);
        initialState.FirstOwnerMembership.OrganizationId.Should().Be(organizationId);
        initialState.FirstOwnerMembership.PersonId.Should().Be(personId);
        initialState.FirstOwnerMembership.Status.Should().Be(OrganizationMembershipStatus.Active);
        initialState.FirstOwnerMembership.StartedAt.Should().Be(now);
        initialState.FirstOwnerMembership.Roles.Should().BeEquivalentTo([OrganizationRole.Owner]);
        initialState.FirstOwnerMembership.HasAuthority(OrganizationRole.Member).Should().BeTrue();

        typeof(Organization)
            .GetProperties()
            .Select(property => property.PropertyType)
            .Should()
            .NotContain(typeof(OrganizationMembership));
    }

    [Fact]
    public void Create_rejects_missing_current_person()
    {
        var create = () => InitialOrganizationState.Create(
            NewOrganizationId(),
            NewMembershipId(),
            "Acme AB",
            NewLegalIdentity(),
            null!,
            NewFrozenTimeProvider());

        create.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_rejects_default_organization_id()
    {
        var create = () => InitialOrganizationState.Create(
            default,
            NewMembershipId(),
            "Acme AB",
            NewLegalIdentity(),
            new StubCurrentPerson(NewPersonId()),
            NewFrozenTimeProvider());

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_default_first_owner_membership_id()
    {
        var create = () => InitialOrganizationState.Create(
            NewOrganizationId(),
            default,
            "Acme AB",
            NewLegalIdentity(),
            new StubCurrentPerson(NewPersonId()),
            NewFrozenTimeProvider());

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_default_current_person_id()
    {
        var create = () => InitialOrganizationState.Create(
            NewOrganizationId(),
            NewMembershipId(),
            "Acme AB",
            NewLegalIdentity(),
            new StubCurrentPerson(default),
            NewFrozenTimeProvider());

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_missing_time_provider()
    {
        var create = () => InitialOrganizationState.Create(
            NewOrganizationId(),
            NewMembershipId(),
            "Acme AB",
            NewLegalIdentity(),
            new StubCurrentPerson(NewPersonId()),
            null!);

        create.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_uses_current_person_contract_as_the_first_owner_source()
    {
        var personId = NewPersonId();

        var initialState = InitialOrganizationState.Create(
            NewOrganizationId(),
            NewMembershipId(),
            "Acme AB",
            NewLegalIdentity(),
            new StubCurrentPerson(personId),
            NewFrozenTimeProvider());

        initialState.FirstOwnerMembership.PersonId.Should().Be(personId);
    }

    private static OrganizationId NewOrganizationId() => new(Guid.NewGuid());

    private static OrganizationMembershipId NewMembershipId() => new(Guid.NewGuid());

    private static PersonId NewPersonId() => new(Guid.NewGuid());

    private static LegalOrganizationIdentity NewLegalIdentity() => LegalOrganizationIdentity.Create("SE", "5560160680");

    private static FrozenTimeProvider NewFrozenTimeProvider() => new(new DateTimeOffset(2026, 9, 10, 12, 30, 0, TimeSpan.Zero));

    private sealed class StubCurrentPerson(PersonId personId) : ICurrentPerson
    {
        public PersonId PersonId { get; } = personId;
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
