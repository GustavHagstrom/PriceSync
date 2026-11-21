using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Version_list_returns_an_empty_page_for_an_unpublished_agreement()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var response = await client.GetAsync($"{scope.Agreement}/versions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        page.GetProperty("items").GetArrayLength().Should().Be(0);
        page.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }
    [Fact]
    public async Task Version_list_keeps_its_position_when_a_new_version_is_published()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var url = $"{scope.Agreement}/versions";
        var request = Publication(scope.List);
        var originals = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            request["coverage"]![0]!["generalDiscount"] = i + 0.1m;
            request["groupDiscounts"] = new JsonArray(new JsonObject { ["code"] = "01", ["discount"] = i });
            using var published = await client.PostAsJsonAsync(url, request);
            published.StatusCode.Should().Be(HttpStatusCode.Created);
            originals.Add(await published.Content.ReadAsStringAsync());
            request["expectedLatestVersionId"] = (await published.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var first = await client.GetFromJsonAsync<JsonElement>($"{url}?pageSize=2");
        first.GetProperty("items").EnumerateArray().Select(item => item.GetRawText())
            .Should().Equal(originals[2], originals[1]);
        var cursor = first.GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNullOrEmpty();

        await using var relationships = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions());
        var relationship = await relationships.TradingRelationships.SingleAsync();
        using var other = await PostAgreement(client, relationship.Id.Value);
        other.StatusCode.Should().Be(HttpStatusCode.Created);
        var wrongAgreement = await client.GetAsync($"{other.Headers.Location}/versions?cursor={cursor}");
        await AssertVersionListValidation(wrongAgreement, "cursor");

        (await client.PostAsJsonAsync(url, request)).StatusCode.Should().Be(HttpStatusCode.Created);
        var last = await client.GetFromJsonAsync<JsonElement>($"{url}?pageSize=2&cursor={cursor}");
        last.GetProperty("items").EnumerateArray().Select(item => item.GetRawText()).Should().Equal(originals[0]);
        last.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=-1", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=1.5", "pageSize")]
    [InlineData("pageSize=", "pageSize")]
    [InlineData("cursor=", "cursor")]
    [InlineData("cursor=!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!", "cursor")]
    public async Task Version_list_validates_query_fields(string query, string field)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        await AssertVersionListValidation(await client.GetAsync($"{scope.Agreement}/versions?{query}"), field);
    }

    [Fact]
    public async Task Version_list_defaults_to_50_and_accepts_a_limit_of_100()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var url = $"{scope.Agreement}/versions";
        var request = Publication(scope.List);
        for (var ordinal = 1; ordinal <= 101; ordinal++)
        {
            using var published = await client.PostAsJsonAsync(url, request);
            published.StatusCode.Should().Be(HttpStatusCode.Created);
            request["expectedLatestVersionId"] = (await published.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var defaults = await client.GetFromJsonAsync<JsonElement>(url);
        defaults.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("ordinal").GetInt32())
            .Should().Equal(Enumerable.Range(52, 50).Reverse());
        var maximum = await client.GetFromJsonAsync<JsonElement>($"{url}?pageSize=100");
        maximum.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("ordinal").GetInt32())
            .Should().Equal(Enumerable.Range(2, 100).Reverse());
        var last = await client.GetFromJsonAsync<JsonElement>($"{url}?cursor={maximum.GetProperty("nextCursor").GetString()}");
        last.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("ordinal").GetInt32()).Should().Equal(1);
        last.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("supplier", 200)]
    [InlineData("customer", 200)]
    [InlineData("other", 404)]
    [InlineData("noMembership", 403)]
    [InlineData("archived", 403)]
    public async Task Version_list_applies_current_authority_after_the_relationship_ends(string scenario, int status)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var publisher = scope.Client;
        (await publisher.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List))).StatusCode.Should().Be(HttpStatusCode.Created);
        await using var relationships = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions());
        var relationship = await relationships.TradingRelationships.SingleAsync();
        relationship.End(DateTimeOffset.UtcNow);
        await relationships.SaveChangesAsync();

        var person = new PersonId(Guid.CreateVersion7());
        var acting = scenario == "customer" ? scope.Customer : relationship.SupplierOrganizationId;
        if (scenario == "other")
            acting = await SeedOrganization(person, "5560004615");
        else if (scenario != "noMembership")
        {
            await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
            db.OrganizationMemberships.Add(OrganizationMembership.Create(new(Guid.CreateVersion7()), acting, person,
                [OrganizationRole.Member], TimeProvider.System));
            if (scenario == "archived") (await db.Organizations.FindAsync(acting))!.Archive(TimeProvider.System);
            await db.SaveChangesAsync();
        }
        await using var readerApp = new ApiApplication(fixture.ConnectionString, person);
        using var reader = readerApp.CreateClient();
        reader.DefaultRequestHeaders.Add("organization_id", acting.ToString());
        using var response = await reader.GetAsync($"{scope.Agreement}/versions");
        ((int)response.StatusCode).Should().Be(status);
        if (status == 200)
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").GetArrayLength().Should().Be(1);
        if (status == 404)
        {
            var absent = await reader.GetAsync($"/api/v1/agreements/{Guid.CreateVersion7()}/versions");
            (await ProblemWithoutTrace(response)).Should().Be(await ProblemWithoutTrace(absent));
        }
    }

    private static async Task AssertVersionListValidation(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("code").GetString().Should().Be("validation_failed");
        detail.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();
    }
}
