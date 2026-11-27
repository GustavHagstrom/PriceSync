using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.PricePublication.Persistence;

namespace PriceSync.Modules.PricePublication.IntegrationTests;

[Collection(PricePublicationPostgreSqlCollection.Name)]
public sealed class PriceListApiTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Owner_can_create_and_read_a_general_list_before_publication()
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        await using var application = new ApiApplication(fixture.ConnectionString, person);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.Value.ToString());
        var response = await client.PostAsJsonAsync("/api/v1/price-lists", Request());
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("id").GetGuid().Version.Should().Be(7);
        created.GetProperty("ownerOrganizationId").GetGuid().Should().Be(supplier.Value);
        created.GetProperty("type").GetString().Should().Be("general");
        created.GetProperty("name").GetString().Should().Be("General SEK");
        created.GetProperty("currency").GetString().Should().Be("SEK");
        created.GetProperty("market").GetString().Should().Be("Sweden");
        created.GetProperty("customerOrganizationId").ValueKind.Should().Be(JsonValueKind.Null);
        var read = await client.GetAsync(response.Headers.Location);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadAsStringAsync()).Should().Be(await response.Content.ReadAsStringAsync());
    }

    private static object Request(string type = "general", Guid? customer = null) => new
    {
        name = "General SEK", type, currency = "SEK", market = "Sweden", customerOrganizationId = customer,
    };

    [Theory]
    [InlineData("member", 403, 200)]
    [InlineData("owner", 201, 200)]
    [InlineData("ended", 403, 403)]
    [InlineData("archived", 403, 403)]
    [InlineData("unrelatedPerson", 403, 403)]
    [InlineData("otherOrganization", 201, 404)]
    [InlineData("anonymous", 401, 401)]
    [InlineData("noOrganization", 400, 400)]
    public async Task List_access_uses_real_organization_membership_authority(string scenario, int createStatus, int readStatus)
    {
        var owner = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(owner);
        await using var ownerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var ownerClient = ownerApp.CreateClient();
        ownerClient.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var original = await ownerClient.PostAsJsonAsync("/api/v1/price-lists", Request());
        original.StatusCode.Should().Be(HttpStatusCode.Created);
        var actor = new PersonId(Guid.CreateVersion7());
        if (scenario is "member" or "ended" or "owner")
        {
            await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
            var membership = OrganizationMembership.Create(new(Guid.CreateVersion7()), supplier, actor,
                [scenario == "owner" ? OrganizationRole.Owner : OrganizationRole.Member], TimeProvider.System);
            if (scenario == "ended") membership.End(TimeProvider.System);
            db.OrganizationMemberships.Add(membership);
            await db.SaveChangesAsync();
        }
        if (scenario == "archived")
        {
            actor = owner;
            await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
            var organization = await db.Organizations.FindAsync(supplier);
            organization!.Archive(TimeProvider.System);
            await db.SaveChangesAsync();
        }
        var actingOrganization = scenario == "otherOrganization"
            ? await SeedOrganization(actor, number: "5560360793") : supplier;
        await using var app = new ApiApplication(fixture.ConnectionString, scenario == "anonymous" ? null : actor);
        using var client = app.CreateClient();
        if (scenario != "noOrganization") client.DefaultRequestHeaders.Add("organization_id", actingOrganization.ToString());
        var create = await client.PostAsJsonAsync("/api/v1/price-lists", Request());
        ((int)create.StatusCode).Should().Be(createStatus, await create.Content.ReadAsStringAsync());
        var read = await client.GetAsync(original.Headers.Location);
        ((int)read.StatusCode).Should().Be(readStatus, await read.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Owner_can_create_a_net_list_bound_to_one_existing_customer()
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), number: "5560360793");
        await using var application = new ApiApplication(fixture.ConnectionString, person);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.Value.ToString());
        var response = await client.PostAsJsonAsync("/api/v1/price-lists", Request("customerSpecificNet", customer.Value));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("type").GetString().Should().Be("customerSpecificNet");
        created.GetProperty("customerOrganizationId").GetGuid().Should().Be(customer.Value);
        var read = await client.GetAsync(response.Headers.Location);
        (await read.Content.ReadAsStringAsync()).Should().Be(await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("OwnerOrganizationId", "'00000000-0000-7000-8000-000000000001'")]
    [InlineData("Type", "'customerSpecificNet'")]
    [InlineData("Currency", "'EUR'")]
    [InlineData("Market", "'Norway'")]
    [InlineData("CustomerOrganizationId", "'00000000-0000-7000-8000-000000000001'")]
    public async Task Persisted_scope_cannot_change_even_through_a_direct_database_write(string column, string value)
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        await using var app = new ApiApplication(fixture.ConnectionString, person);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var response = await client.PostAsJsonAsync("/api/v1/price-lists", Request());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        var id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"update price_publication.price_lists set \"{column}\" = {value} where \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", id);
        var update = async () => await command.ExecuteNonQueryAsync();
        (await update.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        var read = await client.GetAsync(response.Headers.Location);
        (await read.Content.ReadAsStringAsync()).Should().Be(body);
    }

    [Fact]
    public async Task Name_is_display_metadata_and_can_change_without_changing_identity_or_scope()
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        await using var app = new ApiApplication(fixture.ConnectionString, person);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var first = await client.PostAsJsonAsync("/api/v1/price-lists", Request());
        var second = await client.PostAsJsonAsync("/api/v1/price-lists", Request());
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var original = await first.Content.ReadFromJsonAsync<JsonElement>();
        var other = await second.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetGuid();
        other.GetProperty("id").GetGuid().Should().NotBe(id);
        await using (var db = new PricePublicationDbContext(fixture.CreatePublicationOptions()))
        {
            var list = await db.PriceLists.SingleAsync(x => x.Id == new PriceListId(id));
            list.Rename("Renamed");
            await db.SaveChangesAsync();
        }
        var read = await client.GetFromJsonAsync<JsonElement>(first.Headers.Location);
        read.GetProperty("name").GetString().Should().Be("Renamed");
        foreach (var field in new[] { "id", "ownerOrganizationId", "type", "currency", "market", "customerOrganizationId" })
            read.GetProperty(field).ToString().Should().Be(original.GetProperty(field).ToString());
    }

    [Theory]
    [InlineData("blankName")]
    [InlineData("longName")]
    [InlineData("unknownType")]
    [InlineData("externalPrices")]
    [InlineData("invalidCurrency")]
    [InlineData("blankMarket")]
    [InlineData("longMarket")]
    [InlineData("generalWithCustomer")]
    [InlineData("netWithoutCustomer")]
    [InlineData("emptyCustomer")]
    [InlineData("missingCustomer")]
    [InlineData("archivedCustomer")]
    public async Task Invalid_scope_is_rejected_without_creating_a_list(string scenario)
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        var request = new Dictionary<string, object?>
        {
            ["name"] = "List", ["type"] = "general", ["currency"] = "SEK", ["market"] = "Sweden",
        };
        switch (scenario)
        {
            case "blankName": request["name"] = " "; break;
            case "longName": request["name"] = new string('n', 201); break;
            case "unknownType": request["type"] = "unknown"; break;
            case "externalPrices": request["type"] = "customerManagedExternalNet"; break;
            case "invalidCurrency": request["currency"] = "12k"; break;
            case "blankMarket": request["market"] = " "; break;
            case "longMarket": request["market"] = new string('m', 101); break;
            case "generalWithCustomer": request["customerOrganizationId"] = supplier.Value; break;
            case "netWithoutCustomer": request["type"] = "customerSpecificNet"; break;
            default:
                request["type"] = "customerSpecificNet";
                request["customerOrganizationId"] = scenario == "emptyCustomer" ? Guid.Empty : Guid.CreateVersion7();
                if (scenario == "archivedCustomer")
                {
                    var customer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), number: "5560360793");
                    await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
                    var organization = await db.Organizations.FindAsync(customer);
                    organization!.Archive(TimeProvider.System);
                    await db.SaveChangesAsync();
                    request["customerOrganizationId"] = customer.Value;
                }
                break;
        }
        await using var app = new ApiApplication(fixture.ConnectionString, person);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var response = await client.PostAsJsonAsync("/api/v1/price-lists", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        response.Headers.Location.Should().BeNull();
        await using var publication = new PricePublicationDbContext(fixture.CreatePublicationOptions());
        (await publication.PriceLists.CountAsync()).Should().Be(0);
    }

    private async Task<OrganizationId> SeedOrganization(PersonId person, OrganizationRole role = OrganizationRole.Owner,
        string number = "5560160680")
    {
        var organization = Organization.Create(OrganizationId.Create(), "Supplier",
            LegalOrganizationIdentity.Create("SE", number), TimeProvider.System);
        var membership = OrganizationMembership.Create(new(Guid.CreateVersion7()), organization.Id, person,
            [role], TimeProvider.System);
        await using var context = new OrganizationAccessDbContext(fixture.CreateOptions());
        context.Organizations.Add(organization);
        context.OrganizationMemberships.Add(membership);
        await context.SaveChangesAsync();
        return organization.Id;
    }
}
