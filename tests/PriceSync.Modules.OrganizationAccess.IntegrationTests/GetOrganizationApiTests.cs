using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.IntegrationTests;

[Collection(OrganizationAccessPostgreSqlCollection.Name)]
public sealed class GetOrganizationApiTests(OrganizationAccessPostgreSqlFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_organization_requires_authentication()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();

        var response = await client.GetAsync($"/api/v1/organizations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("code").GetString().Should().Be("authentication_required");
    }

    [Fact]
    public async Task Active_member_can_read_active_organization_with_explicit_roles()
    {
        var personId = NewPersonId();
        var organization = await SeedOrganizationAsync(personId, roles: [OrganizationRole.Owner]);

        await using var application = CreateApplication(personId);
        using var client = application.CreateClient();

        var response = await client.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{organization.Id.Value}");

        response.GetProperty("organizationId").GetGuid().Should().Be(organization.Id.Value);
        response.GetProperty("displayName").GetString().Should().Be("Acme AB");
        response.GetProperty("registrationCountry").GetString().Should().Be("SE");
        response.GetProperty("organizationNumber").GetString().Should().Be("5560160680");
        response.GetProperty("status").GetString().Should().Be("active");
        response.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).Should().Equal("owner");
    }

    [Fact]
    public async Task Active_member_can_read_archived_organization()
    {
        var personId = NewPersonId();
        var organization = await SeedOrganizationAsync(personId, archived: true);

        await using var application = CreateApplication(personId);
        using var client = application.CreateClient();

        var response = await client.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{organization.Id.Value}");

        response.GetProperty("status").GetString().Should().Be("archived");
    }

    [Fact]
    public async Task Missing_organization_returns_not_found_problem_details()
    {
        await using var application = CreateApplication(NewPersonId());
        using var client = application.CreateClient();

        var response = await client.GetAsync($"/api/v1/organizations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("code").GetString().Should().Be("organization_not_found");
    }

    [Fact]
    public async Task Inaccessible_organization_returns_same_not_found_problem_details_as_missing_organization()
    {
        var organization = await SeedOrganizationAsync(NewPersonId());

        await using var application = CreateApplication(NewPersonId());
        using var client = application.CreateClient();

        var response = await client.GetAsync($"/api/v1/organizations/{organization.Id.Value}");
        var missingResponse = await client.GetAsync($"/api/v1/organizations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        missingResponse.StatusCode.Should().Be(response.StatusCode);
        missingResponse.Content.Headers.ContentType?.MediaType
            .Should().Be(response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var missingProblem = await missingResponse.Content.ReadFromJsonAsync<JsonElement>();
        ProblemDetailsContract(problem).Should().BeEquivalentTo(ProblemDetailsContract(missingProblem));
    }

    [Fact]
    public async Task Ended_membership_cannot_read_organization()
    {
        var personId = NewPersonId();
        var organization = await SeedOrganizationAsync(personId, activeMembership: false);

        await using var application = CreateApplication(personId);
        using var client = application.CreateClient();

        var response = await client.GetAsync($"/api/v1/organizations/{organization.Id.Value}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Archived_organization_does_not_grant_read_access_without_active_membership()
    {
        var organization = await SeedOrganizationAsync(NewPersonId(), archived: true);

        await using var application = CreateApplication(NewPersonId());
        using var client = application.CreateClient();

        var response = await client.GetAsync($"/api/v1/organizations/{organization.Id.Value}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("organization_not_found");
    }

    private OrganizationAccessApiApplication CreateApplication(PersonId? personId = null)
    {
        return new OrganizationAccessApiApplication(fixture.ConnectionString, personId);
    }

    private async Task<Organization> SeedOrganizationAsync(
        PersonId personId,
        OrganizationRole[]? roles = null,
        bool archived = false,
        bool activeMembership = true)
    {
        var timeProvider = new FrozenTimeProvider(new DateTimeOffset(2026, 9, 16, 8, 30, 0, TimeSpan.Zero));
        var organization = Organization.Create(
            NewOrganizationId(),
            "Acme AB",
            LegalOrganizationIdentity.Create("SE", "556016-0680"),
            timeProvider);

        if (archived)
        {
            organization.Archive(new FrozenTimeProvider(new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero)));
        }

        var membership = OrganizationMembership.Create(
            NewMembershipId(),
            organization.Id,
            personId,
            roles ?? [OrganizationRole.Member],
            timeProvider);

        if (!activeMembership)
        {
            membership.End(new FrozenTimeProvider(new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero)));
        }

        await using var context = new OrganizationAccessDbContext(fixture.CreateOptions());
        context.Organizations.Add(organization);
        context.OrganizationMemberships.Add(membership);
        await context.SaveChangesAsync();

        return organization;
    }

    private static OrganizationId NewOrganizationId() => new(Guid.CreateVersion7());

    private static OrganizationMembershipId NewMembershipId() => new(Guid.CreateVersion7());

    private static PersonId NewPersonId() => new(Guid.NewGuid());

    private static (string? Title, int Status, string? Code) ProblemDetailsContract(JsonElement problem)
    {
        return (
            problem.GetProperty("title").GetString(),
            problem.GetProperty("status").GetInt32(),
            problem.GetProperty("code").GetString());
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
