using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Relationship_list_includes_agreements_without_published_versions()
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var empty = await client.GetFromJsonAsync<JsonElement>($"/api/v1/agreements?tradingRelationshipId={scope.Relationship}");
        empty.GetProperty("items").GetArrayLength().Should().Be(0);
        empty.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        var created = await PostAgreement(client, scope.Relationship.Value);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var response = await client.GetAsync($"/api/v1/agreements?tradingRelationshipId={scope.Relationship}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = page.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        item.GetProperty("id").GetGuid().Should().Be(original.GetProperty("id").GetGuid());
        item.GetProperty("latestVersionId").ValueKind.Should().Be(JsonValueKind.Null);
        page.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Keyset_pagination_uses_identity_to_break_time_ties_and_ignores_newer_insertions()
    {
        var scope = await SeedAgreementScope();
        var clock = new ListTestClock(new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero));
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner, services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var expected = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var created = await PostAgreement(client, scope.Relationship.Value);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            expected.Add((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
        }
        var url = $"/api/v1/agreements?tradingRelationshipId={scope.Relationship}&pageSize=2";
        var first = await client.GetFromJsonAsync<JsonElement>(url);
        first.GetProperty("items").GetArrayLength().Should().Be(2);
        clock.Now = clock.Now.AddHours(1);
        (await PostAgreement(client, scope.Relationship.Value)).StatusCode.Should().Be(HttpStatusCode.Created);
        var actual = first.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString()!).ToList();
        var cursor = first.GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNull();
        var pages = 1;
        while (cursor is not null && pages++ < 5)
        {
            var page = await client.GetFromJsonAsync<JsonElement>($"{url}&cursor={Uri.EscapeDataString(cursor)}");
            actual.AddRange(page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString()!));
            cursor = page.GetProperty("nextCursor").GetString();
        }
        cursor.Should().BeNull();
        actual.Should().Equal(expected.OrderDescending(StringComparer.Ordinal));
    }

    private sealed class ListTestClock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task List_defaults_to_fifty_allows_one_hundred_and_is_scoped_to_one_relationship()
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        for (var i = 0; i < 101; i++)
            (await PostAgreement(client, scope.Relationship.Value)).StatusCode.Should().Be(HttpStatusCode.Created);
        var otherCustomer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560004615");
        var otherRelationship = await SeedRelationship(scope.Supplier, otherCustomer);
        (await PostAgreement(client, otherRelationship.Id.Value)).StatusCode.Should().Be(HttpStatusCode.Created);
        var url = $"/api/v1/agreements?tradingRelationshipId={scope.Relationship}";
        var first = await client.GetFromJsonAsync<JsonElement>(url);
        first.GetProperty("items").GetArrayLength().Should().Be(50);
        var ids = first.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        var second = await client.GetFromJsonAsync<JsonElement>($"{url}&cursor={first.GetProperty("nextCursor").GetString()}");
        second.GetProperty("items").GetArrayLength().Should().Be(50);
        ids.AddRange(second.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        var third = await client.GetFromJsonAsync<JsonElement>($"{url}&cursor={second.GetProperty("nextCursor").GetString()}");
        third.GetProperty("items").GetArrayLength().Should().Be(1);
        third.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        ids.Add(third.GetProperty("items")[0].GetProperty("id").GetGuid());
        ids.Should().HaveCount(101).And.OnlyHaveUniqueItems();
        var largest = await client.GetFromJsonAsync<JsonElement>($"{url}&pageSize=100");
        largest.GetProperty("items").GetArrayLength().Should().Be(100);
        largest.GetProperty("nextCursor").GetString().Should().NotBeNull();
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=-1", "pageSize")]
    [InlineData("pageSize=1.5", "pageSize")]
    [InlineData("pageSize=abc", "pageSize")]
    [InlineData("pageSize=", "pageSize")]
    [InlineData("pageSize=2147483648", "pageSize")]
    [InlineData("cursor=", "cursor")]
    [InlineData("cursor=abc!", "cursor")]
    [InlineData("cursor=YQ", "cursor")]
    [InlineData("badTimestamp", "cursor")]
    [InlineData("nonUtc", "cursor")]
    [InlineData("emptyId", "cursor")]
    [InlineData("tooLong", "cursor")]
    public async Task List_rejects_invalid_pagination_with_field_errors(string input, string field)
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var encoded = input switch
        {
            "badTimestamp" => $"invalid|{Guid.CreateVersion7()}",
            "nonUtc" => $"2026-10-25T10:00:00.0000000+01:00|{Guid.CreateVersion7()}",
            "emptyId" => $"2026-10-25T10:00:00.0000000+00:00|{Guid.Empty}",
            "tooLong" => new string('a', 300), _ => null,
        };
        var query = encoded is null ? input : $"cursor={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(encoded))}";
        var response = await client.GetAsync($"/api/v1/agreements?tradingRelationshipId={scope.Relationship}&{query}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("?tradingRelationshipId=")]
    [InlineData("?tradingRelationshipId=invalid")]
    [InlineData("?tradingRelationshipId=00000000-0000-0000-0000-000000000000")]
    public async Task List_requires_a_relationship_identity(string query)
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        (await client.GetAsync($"/api/v1/agreements{query}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_continuation_cursor_does_not_bypass_lost_membership()
    {
        var scope = await SeedAgreementScope();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        (await PostAgreement(client, scope.Relationship.Value)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await PostAgreement(client, scope.Relationship.Value)).StatusCode.Should().Be(HttpStatusCode.Created);
        var url = $"/api/v1/agreements?tradingRelationshipId={scope.Relationship}&pageSize=1";
        var first = await client.GetFromJsonAsync<JsonElement>(url);
        var cursor = first.GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNull();
        await using (var db = new OrganizationAccessDbContext(fixture.CreateOptions()))
        {
            (await db.OrganizationMemberships.SingleAsync(x => x.PersonId == scope.Owner)).End(TimeProvider.System);
            await db.SaveChangesAsync();
        }
        (await client.GetAsync($"{url}&cursor={cursor}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
