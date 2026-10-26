using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.OrganizationAccess.Contracts;
using Npgsql;
using PriceSync.Modules.Agreements.Persistence;
using FluentAssertions;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    private async Task<(ApiApplication App, HttpClient Client, string Agreement, Guid List, OrganizationId Customer)> PublicationScope()
    {
        var scope = await SeedAgreementScope();
        var app = new ApiApplication(fixture.ConnectionString, scope.Owner);
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var list = await client.PostAsJsonAsync("/api/v1/price-lists", new
        { name = "General", type = "general", currency = "SEK", market = "SE" });
        list.StatusCode.Should().Be(HttpStatusCode.Created);
        var listId = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var agreement = await PostAgreement(client, scope.Relationship.Value);
        agreement.StatusCode.Should().Be(HttpStatusCode.Created);
        return (app, client, agreement.Headers.Location!.ToString(), listId, scope.Customer);
    }

    private static JsonObject Publication(Guid list) => JsonSerializer.SerializeToNode(new
    {
        expectedLatestVersionId = (Guid?)null, validFrom = "2026-10-26", validTo = (string?)null,
        coverage = new[] { new { priceListId = list, type = "general", generalDiscount = 0m } },
        groupDiscounts = Array.Empty<object>(),
    })!.AsObject();

    [Fact]
    public async Task Real_scope_verification_rejects_another_suppliers_list_and_a_net_list_disguised_as_general()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var net = await client.PostAsJsonAsync("/api/v1/price-lists", new
        { name = "Net", type = "customerSpecificNet", currency = "SEK", market = "SE", customerOrganizationId = scope.Customer.Value });
        net.StatusCode.Should().Be(HttpStatusCode.Created);
        var netId = (await net.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var otherOwner = new PersonId(Guid.CreateVersion7());
        var otherSupplier = await SeedOrganization(otherOwner, "5560004615");
        await using var otherApp = new ApiApplication(fixture.ConnectionString, otherOwner);
        using var otherClient = otherApp.CreateClient();
        otherClient.DefaultRequestHeaders.Add("organization_id", otherSupplier.ToString());
        var other = await otherClient.PostAsJsonAsync("/api/v1/price-lists", new
        { name = "Other", type = "general", currency = "SEK", market = "SE" });
        other.StatusCode.Should().Be(HttpStatusCode.Created);
        var otherId = (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var listId in new[] { netId, otherId })
        {
            var response = await client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(listId));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
                .TryGetProperty("coverage[0].priceListId", out _).Should().BeTrue();
        }
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.Versions.CountAsync()).Should().Be(0);
        (await db.PriceAgreements.SingleAsync()).LatestVersionId.Should().BeNull();
    }

    [Theory]
    [InlineData("0.1")]
    [InlineData("99.9")]
    [InlineData("100")]
    public async Task Publication_preserves_allowed_exact_discount_values(string value)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var discount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var request = Publication(scope.List);
        request["coverage"]![0]!["generalDiscount"] = discount;
        var response = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("coverage")[0]
            .GetProperty("generalDiscount").GetDecimal().Should().Be(discount);
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.Coverage.SingleAsync()).GeneralDiscount.Should().Be(discount);
    }

    [Fact]
    public async Task Database_rejects_a_latest_ordinal_without_a_latest_version()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        var change = async () => await db.Database.ExecuteSqlRawAsync("""
            UPDATE agreements.price_agreements SET "LatestOrdinal" = 1
            """);
        (await change.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task Concurrent_first_publications_lock_the_agreement_and_produce_one_created_and_one_conflict()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var initialClient = scope.Client;
        var gate = new CommitGate(false);
        await using var guarded = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate))));
        using var client = guarded.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", initialClient.DefaultRequestHeaders.GetValues("organization_id"));
        var first = client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        Task<HttpResponseMessage> second;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            gate.Isolation.Should().Be(System.Data.IsolationLevel.ReadCommitted);
            second = client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var connection = new NpgsqlConnection(fixture.ConnectionString);
            await connection.OpenAsync(timeout.Token);
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                    WHERE datname = current_database() AND wait_event_type = 'Lock'
                    AND query LIKE '%FROM agreements.price_agreements%FOR UPDATE%')
                """, connection);
            while (!(bool)(await command.ExecuteScalarAsync(timeout.Token))!) await Task.Delay(20, timeout.Token);
            second.IsCompleted.Should().BeFalse();
        }
        finally { gate.Release.TrySetResult(); }
        (await first).StatusCode.Should().Be(HttpStatusCode.Created);
        (await second).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        var version = await db.Versions.SingleAsync();
        (await db.PriceAgreements.SingleAsync()).LatestVersionId.Should().Be(version.Id);
        (await db.Coverage.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Failure_before_commit_rolls_back_version_coverage_and_latest_and_allows_first_publication_again()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var initialClient = scope.Client;
        var gate = new CommitGate(true);
        gate.Release.SetResult();
        await using var guarded = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate))));
        using var client = guarded.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", initialClient.DefaultRequestHeaders.GetValues("organization_id"));
        var failed = await client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        failed.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using (var db = new AgreementsDbContext(fixture.CreateAgreementOptions()))
        {
            (await db.Versions.CountAsync()).Should().Be(0);
            (await db.Coverage.CountAsync()).Should().Be(0);
            var agreement = await db.PriceAgreements.SingleAsync();
            agreement.LatestVersionId.Should().BeNull();
            agreement.LatestOrdinal.Should().Be(0);
        }
        var retry = await client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        retry.StatusCode.Should().Be(HttpStatusCode.Created, await retry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_list_scope_and_unavailable_verification_never_publish()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var invalid = await client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(Guid.CreateVersion7()));
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("coverage[0].priceListId", out _).Should().BeTrue();
        await using var unavailable = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPriceListScopeVerifier>();
            services.AddSingleton<IPriceListScopeVerifier, UnavailableListVerifier>();
        }));
        using var unavailableClient = unavailable.CreateClient();
        unavailableClient.DefaultRequestHeaders.Add("organization_id", client.DefaultRequestHeaders.GetValues("organization_id"));
        var response = await unavailableClient.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("agreement_list_verification_unavailable");
        problem.GetProperty("retryable").GetBoolean().Should().BeTrue();
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.Versions.CountAsync()).Should().Be(0);
        (await db.Coverage.CountAsync()).Should().Be(0);
        (await db.PriceAgreements.SingleAsync()).LatestVersionId.Should().BeNull();
    }

    private sealed class UnavailableListVerifier : IPriceListScopeVerifier
    {
        public Task<PriceListVerificationResult> VerifyAsync(OrganizationId supplier, OrganizationId customer,
            IReadOnlyCollection<PriceListReference> references, CancellationToken cancellationToken = default) =>
            Task.FromResult<PriceListVerificationResult>(new PriceListVerificationResult.Unavailable());
    }

    [Fact]
    public async Task Successful_publication_updates_the_durable_agreement_latest_pointer_and_repeated_first_attempt_conflicts()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var request = Publication(scope.List);
        var published = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var version = await published.Content.ReadFromJsonAsync<JsonElement>();
        var read = await client.GetFromJsonAsync<JsonElement>(scope.Agreement);
        read.GetProperty("latestVersionId").GetGuid().Should().Be(version.GetProperty("id").GetGuid());
        var retry = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("agreement_publication_conflict");
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        var stored = await db.Versions.SingleAsync();
        stored.Id.Value.Should().Be(version.GetProperty("id").GetGuid());
        stored.PublishedAt.Should().Be(version.GetProperty("publishedAt").GetDateTimeOffset());
        stored.Midpoint.Should().Be("AwayFromZero");
        stored.RoundingStage.Should().Be("OnceAfterDiscountWithoutIntermediateRounding");
        stored.DecimalPlaces.Should().Be(4);
        stored.PreservePriceQuantityAndUnitOfMeasure.Should().BeTrue();
        (await db.Coverage.SingleAsync()).GeneralDiscount.Should().Be(0);
        (await db.PriceAgreements.SingleAsync()).LatestOrdinal.Should().Be(1);
    }

    [Fact]
    public async Task Unsupported_coverage_and_group_terms_are_rejected_without_partial_publication()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        foreach (var scenario in new[] { "missing", "empty", "multiple", "net", "unknownType", "emptyId", "groups", "malformedGroups" })
        {
            var request = Publication(scope.List);
            var field = "coverage";
            switch (scenario)
            {
                case "missing": request.Remove("coverage"); break;
                case "empty": request["coverage"] = new JsonArray(); break;
                case "multiple": request["coverage"]!.AsArray().Add(request["coverage"]![0]!.DeepClone()); break;
                case "net": request["coverage"]![0]!["type"] = "customerSpecificNet"; field = "coverage[0].type"; break;
                case "unknownType": request["coverage"]![0]!["type"] = "invalid"; field = "coverage[0].type"; break;
                case "emptyId": request["coverage"]![0]!["priceListId"] = Guid.Empty; field = "coverage[0].priceListId"; break;
                case "groups": request["groupDiscounts"] = JsonNode.Parse("[{\"code\":\"01\",\"discount\":1}]"); field = "groupDiscounts"; break;
                case "malformedGroups": request["groupDiscounts"] = new JsonObject(); field = "groupDiscounts"; break;
            }
            var response = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, scenario);
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
                .TryGetProperty(field, out _).Should().BeTrue(scenario);
        }
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.Versions.CountAsync()).Should().Be(0);
        (await db.PriceAgreements.SingleAsync()).LatestVersionId.Should().BeNull();
    }

    [Fact]
    public async Task General_discount_is_required_and_rejects_out_of_range_or_excessive_precision()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        foreach (var value in new decimal?[] { null, -0.1m, 100.1m, 0.01m })
        {
            var request = Publication(scope.List);
            request["coverage"]![0]!["generalDiscount"] = JsonValue.Create(value);
            var response = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
                .TryGetProperty("coverage[0].generalDiscount", out _).Should().BeTrue();
        }
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.Versions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Publication_requires_a_valid_whole_version_validity_period()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        foreach (var field in new[] { "validFrom", "validTo" })
        {
            var request = Publication(scope.List);
            if (field == "validFrom") request.Remove(field);
            else request[field] = "2026-10-25";
            var response = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
                .TryGetProperty(field, out _).Should().BeTrue();
        }
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.Versions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task First_publication_requires_an_explicit_null_predecessor()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var request = Publication(scope.List);
        request.Remove("expectedLatestVersionId");
        var response = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").TryGetProperty("expectedLatestVersionId", out _).Should().BeTrue();
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.Versions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Supplier_owner_publishes_first_version_with_explicit_zero_discount()
    {
        var person = new OrganizationAccess.Contracts.PersonId(Guid.CreateVersion7());
        var supplier = await SeedOrganization(person);
        var customer = await SeedOrganization(new(Guid.CreateVersion7()), "5560360793");
        var relationship = await SeedRelationship(supplier, customer);
        await using var app = new ApiApplication(fixture.ConnectionString, person);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var list = await client.PostAsJsonAsync("/api/v1/price-lists", new
        { name = "General", type = "general", currency = "SEK", market = "SE" });
        list.StatusCode.Should().Be(HttpStatusCode.Created, await list.Content.ReadAsStringAsync());
        var listId = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var agreement = await PostAgreement(client, relationship.Id.Value);
        var before = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync($"{agreement.Headers.Location}/versions", new
        {
            expectedLatestVersionId = (Guid?)null, validFrom = "2026-10-26", validTo = "2026-10-26",
            coverage = new[] { new { priceListId = listId, type = "general", generalDiscount = 0m } },
            groupDiscounts = Array.Empty<object>(),
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var version = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = version.GetProperty("id").GetGuid();
        id.Version.Should().Be(7);
        response.Headers.Location!.ToString().Should().Be($"{agreement.Headers.Location}/versions/{id}");
        version.GetProperty("ordinal").GetInt64().Should().Be(1);
        version.GetProperty("publishedByPersonId").GetGuid().Should().Be(person.Value);
        version.GetProperty("publishedAt").GetDateTimeOffset().Should().BeOnOrAfter(before);
        version.GetProperty("validFrom").GetString().Should().Be("2026-10-26");
        version.GetProperty("validTo").GetString().Should().Be("2026-10-26");
        version.GetProperty("predecessorVersionId").ValueKind.Should().Be(JsonValueKind.Null);
        version.GetProperty("tradingRelationshipId").GetGuid().Should().Be(relationship.Id.Value);
        version.GetProperty("coverage")[0].GetProperty("priceListId").GetGuid().Should().Be(listId);
        version.GetProperty("coverage")[0].GetProperty("generalDiscount").GetDecimal().Should().Be(0);
        var rounding = version.GetProperty("rounding");
        rounding.GetProperty("decimalPlaces").GetInt32().Should().Be(4);
        rounding.GetProperty("midpoint").GetString().Should().Be("AwayFromZero");
        rounding.GetProperty("stage").GetString().Should().Be("OnceAfterDiscountWithoutIntermediateRounding");
        rounding.GetProperty("preservePriceQuantityAndUnitOfMeasure").GetBoolean().Should().BeTrue();
    }
}
