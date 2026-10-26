using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    private static async Task<HttpResponseMessage> PostAgreement(HttpClient client, Guid relationshipId, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agreements")
        {
            Content = JsonContent.Create(new { tradingRelationshipId = relationshipId }),
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Creation_requires_an_idempotency_key()
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560360793");
        var relationship = await SeedRelationship(supplier, customer);
        await using var app = new ApiApplication(fixture.ConnectionString, person);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());

        var response = await client.PostAsJsonAsync("/api/v1/agreements", new { tradingRelationshipId = relationship.Id.Value });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Matching_retry_replays_the_original_success_across_application_instances()
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560360793");
        var relationship = await SeedRelationship(supplier, customer);
        await using var app = new ApiApplication(fixture.ConnectionString, person);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var original = await PostAgreement(client, relationship.Id.Value, "repeat");
        original.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var restarted = new ApiApplication(fixture.ConnectionString, person, services =>
            services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => options.SerializerOptions.WriteIndented = true));
        using var retryClient = restarted.CreateClient();
        retryClient.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var replay = await PostAgreement(retryClient, relationship.Id.Value, "repeat");

        replay.StatusCode.Should().Be(original.StatusCode);
        replay.Headers.Location.Should().Be(original.Headers.Location);
        (await replay.Content.ReadAsStringAsync()).Should().Be(await original.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Same_key_with_another_authorized_relationship_conflicts_and_preserves_original_replay()
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560360793");
        var otherCustomer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560004615");
        var first = await SeedRelationship(supplier, customer);
        var second = await SeedRelationship(supplier, otherCustomer);
        await using var app = new ApiApplication(fixture.ConnectionString, person);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var original = await PostAgreement(client, first.Id.Value, "same-key");
        original.StatusCode.Should().Be(HttpStatusCode.Created);

        var conflict = await PostAgreement(client, second.Id.Value, "same-key");
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("agreement_creation_key_conflict");
        var replay = await PostAgreement(client, first.Id.Value, "same-key");
        (await replay.Content.ReadAsStringAsync()).Should().Be(await original.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("long")]
    [InlineData("multiple")]
    [InlineData("control")]
    public async Task Invalid_idempotency_keys_are_rejected_without_creating_an_agreement(string scenario)
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agreements")
        {
            Content = JsonContent.Create(new { tradingRelationshipId = scope.Relationship.Value }),
        };
        var keys = scenario switch
        {
            "empty" => new[] { "" }, "long" => [new string('k', 201)],
            "control" => ["bad\0key"], _ => ["first", "second"],
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", keys);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.PriceAgreements.CountAsync()).Should().Be(0);
        (await db.CreationIdempotencyRecords.CountAsync()).Should().Be(0);
    }

    private async Task<(PersonId Owner, OrganizationId Supplier, OrganizationId Customer, TradingRelationshipId Relationship)> SeedAgreementScope()
    {
        var owner = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(owner);
        var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560360793");
        var relationship = await SeedRelationship(supplier, customer);
        return (owner, supplier, customer, relationship.Id);
    }

    [Fact]
    public async Task Keys_are_case_sensitive_and_accept_both_length_limits()
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var locations = new HashSet<Uri>();
        foreach (var key in new[] { "a", "A", new string('k', 200) })
        {
            var response = await PostAgreement(client, scope.Relationship.Value, key);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            locations.Add(response.Headers.Location!).Should().BeTrue();
            var replay = await PostAgreement(client, scope.Relationship.Value, key);
            replay.Headers.Location.Should().Be(response.Headers.Location);
            (await replay.Content.ReadAsStringAsync()).Should().Be(await response.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData("person")]
    [InlineData("organization")]
    public async Task Keys_are_scoped_to_the_person_and_acting_organization(string changedScope)
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var original = await PostAgreement(client, scope.Relationship.Value, "shared-key");
        original.StatusCode.Should().Be(HttpStatusCode.Created);
        var person = scope.Owner;
        var acting = scope.Supplier;
        var relationship = scope.Relationship;
        if (changedScope == "person")
        {
            person = new PersonId(Guid.CreateVersion7());
            await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
            db.OrganizationMemberships.Add(OrganizationMembership.Create(new(Guid.CreateVersion7()), acting, person,
                [OrganizationRole.Owner], TimeProvider.System));
            await db.SaveChangesAsync();
        }
        else
        {
            acting = await SeedOrganization(person, "5560004615");
            relationship = (await SeedRelationship(acting, scope.Customer)).Id;
        }
        await using var otherApp = new ApiApplication(fixture.ConnectionString, person);
        using var otherClient = otherApp.CreateClient();
        otherClient.DefaultRequestHeaders.Add("organization_id", acting.ToString());
        var separate = await PostAgreement(otherClient, relationship.Value, "shared-key");
        separate.StatusCode.Should().Be(HttpStatusCode.Created);
        separate.Headers.Location.Should().NotBe(original.Headers.Location);
    }

    [Theory]
    [InlineData("membership", 403)]
    [InlineData("organization", 403)]
    [InlineData("relationship", 409)]
    [InlineData("lookupFailure", 500)]
    public async Task Replay_rechecks_current_authority_and_relationship_access(string change, int expectedStatus)
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var original = await PostAgreement(client, scope.Relationship.Value, "recheck");
        original.StatusCode.Should().Be(HttpStatusCode.Created);
        if (change == "relationship")
        {
            await using var db = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions());
            (await db.TradingRelationships.FindAsync(scope.Relationship))!.End(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        else if (change is "membership" or "organization")
        {
            await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
            if (change == "organization") (await db.Organizations.FindAsync(scope.Supplier))!.Archive(TimeProvider.System);
            else (await db.OrganizationMemberships.SingleAsync(x => x.PersonId == scope.Owner)).End(TimeProvider.System);
            await db.SaveChangesAsync();
        }
        await using var retryApp = new ApiApplication(fixture.ConnectionString, scope.Owner, services =>
        {
            if (change != "lookupFailure") return;
            services.RemoveAll<ITradingRelationshipReader>();
            services.AddSingleton<ITradingRelationshipReader>(new UnavailableRelationshipReader());
        });
        using var retry = retryApp.CreateClient();
        retry.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        ((int)(await PostAgreement(retry, scope.Relationship.Value, "recheck")).StatusCode).Should().Be(expectedStatus);
        await using var agreements = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await agreements.PriceAgreements.CountAsync()).Should().Be(1);
        (await agreements.CreationIdempotencyRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Retry_compares_the_normalized_relationship_identity()
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var original = await PostAgreement(client, scope.Relationship.Value, "normalize");
        original.StatusCode.Should().Be(HttpStatusCode.Created);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agreements")
        {
            Content = JsonContent.Create(new { tradingRelationshipId = scope.Relationship.Value.ToString("B").ToUpperInvariant() }),
        };
        request.Headers.Add("Idempotency-Key", "normalize");
        var replay = await client.SendAsync(request);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.Location.Should().Be(original.Headers.Location);
        (await replay.Content.ReadAsStringAsync()).Should().Be(await original.Content.ReadAsStringAsync());
    }
}
