using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.IntegrationTests;

[Collection(OrganizationAccessPostgreSqlCollection.Name)]
public sealed class ListMyOrganizationsApiTests(OrganizationAccessPostgreSqlFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task List_my_organizations_requires_authentication()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/v1/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("code").GetString().Should().Be("authentication_required");
    }

    [Fact]
    public async Task List_my_organizations_includes_only_organizations_where_caller_has_active_membership()
    {
        var personId = NewPersonId();
        var accessible = await SeedOrganizationAsync("Accessible AB", "556016-0680", personId);
        await SeedOrganizationAsync("Someone Else AB", "556000-4615", NewPersonId());
        await SeedOrganizationAsync("Ended AB", "556012-5790", personId, activeMembership: false);

        await using var application = CreateApplication(personId);
        using var client = application.CreateClient();

        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/organizations");

        var organizations = response.EnumerateArray().ToArray();
        organizations.Should().HaveCount(1);
        organizations[0].GetProperty("organizationId").GetGuid().Should().Be(accessible.Id.Value);
    }

    [Fact]
    public async Task List_my_organizations_includes_archived_organizations()
    {
        var personId = NewPersonId();
        var archived = await SeedOrganizationAsync("Archived AB", "556016-0680", personId, archived: true);

        await using var application = CreateApplication(personId);
        using var client = application.CreateClient();

        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/organizations");

        var organization = response.EnumerateArray().Should().ContainSingle().Subject;
        organization.GetProperty("organizationId").GetGuid().Should().Be(archived.Id.Value);
        organization.GetProperty("status").GetString().Should().Be("archived");
    }

    [Fact]
    public async Task List_my_organizations_returns_empty_collection_when_caller_has_no_active_memberships()
    {
        await SeedOrganizationAsync("Acme AB", "556016-0680", NewPersonId());

        await using var application = CreateApplication(NewPersonId());
        using var client = application.CreateClient();

        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/organizations");

        response.ValueKind.Should().Be(JsonValueKind.Array);
        response.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task List_my_organizations_returns_explicit_roles_and_canonical_identity()
    {
        var personId = NewPersonId();
        var organization = await SeedOrganizationAsync(
            "Acme AB",
            "556016-0680",
            personId,
            roles: [OrganizationRole.Owner, OrganizationRole.Member]);

        await using var application = CreateApplication(personId);
        using var client = application.CreateClient();

        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/organizations");

        var item = response.EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("organizationId").GetGuid().Should().Be(organization.Id.Value);
        item.GetProperty("displayName").GetString().Should().Be("Acme AB");
        item.GetProperty("registrationCountry").GetString().Should().Be("SE");
        item.GetProperty("organizationNumber").GetString().Should().Be("5560160680");
        item.GetProperty("status").GetString().Should().Be("active");
        item.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .Should().Equal("member", "owner");
    }

    [Fact]
    public async Task List_my_organizations_orders_active_organizations_first_then_by_display_name()
    {
        var personId = NewPersonId();
        await SeedOrganizationAsync("Zeta AB", "556016-0680", personId);
        await SeedOrganizationAsync("Beta AB", "556000-4615", personId, archived: true);
        await SeedOrganizationAsync("Alpha AB", "556012-5790", personId);

        await using var application = CreateApplication(personId);
        using var client = application.CreateClient();

        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/organizations");

        response.EnumerateArray()
            .Select(item => item.GetProperty("displayName").GetString())
            .Should().Equal("Alpha AB", "Zeta AB", "Beta AB");
    }

    [Fact]
    public async Task List_my_organizations_uses_organization_id_as_a_stable_ordering_tiebreaker()
    {
        var personId = NewPersonId();
        var firstId = new OrganizationId(Guid.Parse("01990000-0000-7000-8000-000000000001"));
        var secondId = new OrganizationId(Guid.Parse("01990000-0000-7000-8000-000000000002"));
        await SeedOrganizationAsync("Acme AB", "556016-0680", personId, organizationId: secondId);
        await SeedOrganizationAsync("Acme AB", "556000-4615", personId, organizationId: firstId);

        await using var application = CreateApplication(personId);
        using var client = application.CreateClient();

        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/organizations");

        response.EnumerateArray()
            .Select(item => item.GetProperty("organizationId").GetGuid())
            .Should().Equal(firstId.Value, secondId.Value);
    }

    private OrganizationAccessApiApplication CreateApplication(PersonId? personId = null)
    {
        return new OrganizationAccessApiApplication(fixture.ConnectionString, personId);
    }

    private async Task<Organization> SeedOrganizationAsync(
        string displayName,
        string organizationNumber,
        PersonId personId,
        OrganizationRole[]? roles = null,
        bool archived = false,
        bool activeMembership = true,
        OrganizationId? organizationId = null)
    {
        var timeProvider = new FrozenTimeProvider(new DateTimeOffset(2026, 9, 16, 8, 30, 0, TimeSpan.Zero));
        var organization = Organization.Create(
            organizationId ?? NewOrganizationId(),
            displayName,
            LegalOrganizationIdentity.Create("SE", organizationNumber),
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

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class OrganizationAccessApiApplication(
        string connectionString,
        PersonId? personId) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrganizationAccess"] = connectionString,
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                    .AddScheme<TestAuthenticationOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        options => { });

                services.PostConfigure<TestAuthenticationOptions>(options =>
                {
                    options.PersonId = personId;
                });
            });
        }
    }

    private sealed class TestAuthenticationOptions : AuthenticationSchemeOptions
    {
        public PersonId? PersonId { get; set; }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<TestAuthenticationOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Options.PersonId is not { } personId)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim("pricesync/person_id", personId.Value.ToString()),
            };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
