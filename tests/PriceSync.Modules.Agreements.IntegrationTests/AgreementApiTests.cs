using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

[Collection(AgreementsPostgreSqlCollection.Name)]
public sealed class AgreementApiTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Supplier_owner_can_create_and_read_a_stable_agreement_without_terms()
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560360793");
        var relationship = TradingRelationship.Create(supplier, customer, DateTimeOffset.UtcNow);
        await using (var db = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions()))
        {
            db.TradingRelationships.Add(relationship);
            await db.SaveChangesAsync();
        }
        await using var app = new ApiApplication(fixture.ConnectionString, person);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var before = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync("/api/v1/agreements", new { tradingRelationshipId = relationship.Id.Value });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("id").GetGuid().Version.Should().Be(7);
        result.GetProperty("tradingRelationshipId").GetGuid().Should().Be(relationship.Id.Value);
        result.GetProperty("createdByPersonId").GetGuid().Should().Be(person.Value);
        result.GetProperty("createdAt").GetDateTimeOffset().Should().BeOnOrAfter(before);
        result.GetProperty("latestVersionId").ValueKind.Should().Be(JsonValueKind.Null);
        var read = await client.GetAsync(response.Headers.Location);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadAsStringAsync()).Should().Be(await response.Content.ReadAsStringAsync());
        var another = await client.PostAsJsonAsync("/api/v1/agreements", new { tradingRelationshipId = relationship.Id.Value });
        another.StatusCode.Should().Be(HttpStatusCode.Created);
        var other = await another.Content.ReadFromJsonAsync<JsonElement>();
        other.GetProperty("id").GetGuid().Should().NotBe(result.GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("supplierMember", 403, 200)]
    [InlineData("customerOwner", 404, 200)]
    [InlineData("customerMember", 403, 200)]
    [InlineData("unrelatedOwner", 404, 404)]
    [InlineData("noMembership", 403, 403)]
    [InlineData("endedMembership", 403, 403)]
    [InlineData("archived", 403, 403)]
    [InlineData("anonymous", 401, 401)]
    [InlineData("noOrganization", 400, 400)]
    public async Task Current_authority_and_relationship_party_scope_control_create_and_read(
        string scenario, int createStatus, int readStatus)
    {
        var owner = new PersonId(Guid.CreateVersion7());
        var actor = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(owner);
        var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560360793");
        var relationship = await SeedRelationship(supplier, customer);
        await using var ownerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var ownerClient = ownerApp.CreateClient();
        ownerClient.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        const string route = "/api/v1/agreements";
        var original = await ownerClient.PostAsJsonAsync(route, new { tradingRelationshipId = relationship.Id.Value });
        original.StatusCode.Should().Be(HttpStatusCode.Created);
        var acting = scenario.StartsWith("customer", StringComparison.Ordinal) ? customer : supplier;
        if (scenario == "unrelatedOwner")
            acting = await SeedOrganization(actor, "5560004615");
        else if (scenario is "supplierMember" or "customerMember" or "customerOwner" or "endedMembership")
        {
            await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
            var membership = OrganizationMembership.Create(new(Guid.CreateVersion7()), acting, actor,
                [scenario == "customerOwner" ? OrganizationRole.Owner : OrganizationRole.Member], TimeProvider.System);
            if (scenario == "endedMembership") membership.End(TimeProvider.System);
            db.OrganizationMemberships.Add(membership);
            await db.SaveChangesAsync();
        }
        else if (scenario == "archived")
        {
            actor = owner;
            await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
            (await db.Organizations.FindAsync(supplier))!.Archive(TimeProvider.System);
            await db.SaveChangesAsync();
        }
        await using var app = new ApiApplication(fixture.ConnectionString, scenario == "anonymous" ? null : actor);
        using var client = app.CreateClient();
        if (scenario != "noOrganization") client.DefaultRequestHeaders.Add("organization_id", acting.ToString());
        var create = await client.PostAsJsonAsync(route, new { tradingRelationshipId = relationship.Id.Value });
        ((int)create.StatusCode).Should().Be(createStatus, await create.Content.ReadAsStringAsync());
        var read = await client.GetAsync(original.Headers.Location);
        ((int)read.StatusCode).Should().Be(readStatus, await read.Content.ReadAsStringAsync());
        if (readStatus == 404)
        {
            var unknown = await client.GetAsync($"/api/v1/agreements/{Guid.CreateVersion7()}");
            (await ProblemWithoutTrace(read)).Should().Be(await ProblemWithoutTrace(unknown));
            var unknownCreate = await client.PostAsJsonAsync(route, new { tradingRelationshipId = Guid.CreateVersion7() });
            (await ProblemWithoutTrace(create)).Should().Be(await ProblemWithoutTrace(unknownCreate));
        }
    }

    [Fact]
    public async Task Ended_relationship_remains_readable_but_rejects_new_agreements()
    {
        var owner = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(owner);
        var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560360793");
        var relationship = await SeedRelationship(supplier, customer);
        await using var app = new ApiApplication(fixture.ConnectionString, owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        const string route = "/api/v1/agreements";
        var original = await client.PostAsJsonAsync(route, new { tradingRelationshipId = relationship.Id.Value });
        original.StatusCode.Should().Be(HttpStatusCode.Created);
        await using (var db = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions()))
        {
            (await db.TradingRelationships.FindAsync(relationship.Id))!.End(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        var rejected = await client.PostAsJsonAsync(route, new { tradingRelationshipId = relationship.Id.Value });
        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("agreement_relationship_ended");
        var read = await client.GetAsync(original.Headers.Location);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadAsStringAsync()).Should().Be(await original.Content.ReadAsStringAsync());
    }

    private async Task<TradingRelationship> SeedRelationship(OrganizationId supplier, OrganizationId customer)
    {
        var relationship = TradingRelationship.Create(supplier, customer, DateTimeOffset.UtcNow);
        await using var db = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions());
        db.TradingRelationships.Add(relationship);
        await db.SaveChangesAsync();
        return relationship;
    }

    [Fact]
    public async Task Technical_relationship_lookup_failure_does_not_grant_access_or_create_an_agreement()
    {
        var owner = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(owner);
        var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560360793");
        var relationship = await SeedRelationship(supplier, customer);
        const string route = "/api/v1/agreements";
        await using var healthy = new ApiApplication(fixture.ConnectionString, owner);
        using var healthyClient = healthy.CreateClient();
        healthyClient.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var original = await healthyClient.PostAsJsonAsync(route, new { tradingRelationshipId = relationship.Id.Value });
        original.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var failed = new ApiApplication(fixture.ConnectionString, owner, services =>
        {
            services.RemoveAll<ITradingRelationshipReader>();
            services.AddSingleton<ITradingRelationshipReader>(new UnavailableRelationshipReader());
        });
        using var client = failed.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        (await client.PostAsJsonAsync(route, new { tradingRelationshipId = relationship.Id.Value })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await client.GetAsync(original.Headers.Location)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using var db = new Persistence.AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.PriceAgreements.CountAsync()).Should().Be(1);
    }

    private sealed class UnavailableRelationshipReader : ITradingRelationshipReader
    {
        public Task<TradingRelationshipDetails?> GetAsync(TradingRelationshipId relationshipId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Relationship lookup unavailable.");
    }

    private static async Task<string> ProblemWithoutTrace(HttpResponseMessage response)
    {
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        problem.Remove("traceId");
        return problem.ToJsonString();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"tradingRelationshipId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"tradingRelationshipId\":\"invalid\"}")]
    public async Task Creation_requires_a_relationship_identity_in_the_request_body(string json)
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        await using var app = new ApiApplication(fixture.ConnectionString, person);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/agreements", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var db = new Persistence.AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.PriceAgreements.CountAsync()).Should().Be(0);
    }

    private async Task<OrganizationId> SeedOrganization(PersonId person, string number = "5560160680")
    {
        var organization = Organization.Create(OrganizationId.Create(), "Party",
            LegalOrganizationIdentity.Create("SE", number), TimeProvider.System);
        var membership = OrganizationMembership.Create(new(Guid.CreateVersion7()), organization.Id, person,
            [OrganizationRole.Owner], TimeProvider.System);
        await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
        db.Organizations.Add(organization);
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();
        return organization.Id;
    }
}
