using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        var authorizer = new OrganizationAuthorizer(context, NullLogger<OrganizationAuthorizer>.Instance);

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
        var authorizer = new OrganizationAuthorizer(context, NullLogger<OrganizationAuthorizer>.Instance);

        var hasCapability = await authorizer.HasCapabilityAsync(
            personId,
            organization.Id,
            OrganizationCapability.ManageTradingRelationships);

        hasCapability.Should().BeFalse();
    }

    [Theory]
    [InlineData("archived", "OrganizationArchived")]
    [InlineData("missing_organization", "OrganizationNotFound")]
    [InlineData("missing_membership", "NoActiveMembership")]
    [InlineData("member_role", "RequiredRoleMissing")]
    public async Task Authorization_denials_preserve_the_internal_reason_and_correlation(
        string scenario, string expectedReason)
    {
        var personId = new PersonId(Guid.CreateVersion7());
        var organization = await SeedOwnerMembershipAsync(personId,
            archived: scenario == "archived",
            role: scenario == "member_role" ? OrganizationRole.Member : OrganizationRole.Owner);
        var actingPerson = scenario == "missing_membership" ? new PersonId(Guid.CreateVersion7()) : personId;
        var actingOrganization = scenario == "missing_organization" ? new OrganizationId(Guid.CreateVersion7()) : organization.Id;
        var logger = new RecordingAuthorizationLogger();
        await using var context = new OrganizationAccessDbContext(fixture.CreateOptions());
        var authorizer = new OrganizationAuthorizer(context, logger);
        using var activity = new Activity("EndTradingRelationship").SetIdFormat(ActivityIdFormat.W3C).Start();

        var allowed = await authorizer.HasCapabilityAsync(
            actingPerson, actingOrganization, OrganizationCapability.ManageTradingRelationships);

        allowed.Should().BeFalse();
        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry["Outcome"].Should().Be("Denied");
        entry["Reason"].Should().Be(expectedReason);
        entry["PersonId"].Should().Be(actingPerson.Value);
        entry["OrganizationId"].Should().Be(actingOrganization.Value);
        entry["Capability"].Should().Be(OrganizationCapability.ManageTradingRelationships);
        entry["TraceId"].Should().Be(activity.TraceId.ToString());
    }

    private sealed class RecordingAuthorizationLogger : ILogger<OrganizationAuthorizer>
    {
        public List<Dictionary<string, object?>> Entries { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                Entries.Add(fields.ToDictionary(field => field.Key, field => field.Value));
        }
    }

    private async Task<Organization> SeedOwnerMembershipAsync(PersonId personId, bool archived = false, OrganizationRole role = OrganizationRole.Owner)
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
            [role],
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
