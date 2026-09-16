using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("organization_not_found");
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
