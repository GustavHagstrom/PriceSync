using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Features.Authorization;
using PriceSync.Modules.OrganizationAccess.Persistence;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.IntegrationTests;

[Collection(OrganizationAccessPostgreSqlCollection.Name)]
public sealed class OrganizationAuthorizationTests(OrganizationAccessPostgreSqlFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Active_owner_has_relationship_management_capability()
    {
        var personId = new PersonId(Guid.CreateVersion7());
        var organization = await SeedOwnerMembershipAsync(personId);
        await using var context = new OrganizationAccessDbContext(fixture.CreateOptions());
        var authorizer = new OrganizationAuthorizer(context);

        var hasCapability = await authorizer.HasCapabilityAsync(
            personId,
            organization.Id,
            OrganizationCapability.ManageTradingRelationships);

        hasCapability.Should().BeTrue();
    }

    [Fact]
    public async Task Archived_organization_does_not_grant_relationship_management_capability()
    {
        var personId = new PersonId(Guid.CreateVersion7());
        var organization = await SeedOwnerMembershipAsync(personId, archived: true);
        await using var context = new OrganizationAccessDbContext(fixture.CreateOptions());
        var authorizer = new OrganizationAuthorizer(context);

        var hasCapability = await authorizer.HasCapabilityAsync(
            personId,
            organization.Id,
            OrganizationCapability.ManageTradingRelationships);

        hasCapability.Should().BeFalse();
    }

    private async Task<Organization> SeedOwnerMembershipAsync(PersonId personId, bool archived = false)
    {
        var createdAt = new DateTimeOffset(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);
        var organization = Organization.Create(
            new OrganizationId(Guid.CreateVersion7()),
            "Acme AB",
            LegalOrganizationIdentity.Create("SE", "5560160680"),
            new FrozenTimeProvider(createdAt));
        var membership = OrganizationMembership.Create(
            new OrganizationMembershipId(Guid.CreateVersion7()),
            organization.Id,
            personId,
            [OrganizationRole.Owner],
            new FrozenTimeProvider(createdAt));

        if (archived)
        {
            organization.Archive(new FrozenTimeProvider(createdAt.AddHours(1)));
        }

        await using var context = new OrganizationAccessDbContext(fixture.CreateOptions());
        context.Organizations.Add(organization);
        context.OrganizationMemberships.Add(membership);
        await context.SaveChangesAsync();

        return organization;
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
