using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Customer_owner_can_change_the_selection_and_both_parties_and_consumers_can_read_it()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var publication = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        publication.StatusCode.Should().Be(HttpStatusCode.Created);
        var published = await publication.Content.ReadFromJsonAsync<JsonElement>();
        var versionId = published.GetProperty("id").GetGuid();
        var relationshipId = published.GetProperty("tradingRelationshipId").GetGuid();
        var url = $"/api/v1/trading-relationships/{relationshipId}/agreement-selection";
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);

        (await supplier.GetFromJsonAsync<JsonElement>(url)).GetProperty("activeVersionId").ValueKind
            .Should().Be(JsonValueKind.Null);
        using var activated = await customer.PutAsJsonAsync(url, new { fromVersionId = (Guid?)null, toVersionId = versionId });
        activated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await activated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activeVersionId").GetGuid().Should().Be(versionId);
        (await supplier.GetFromJsonAsync<JsonElement>(url)).GetProperty("activeVersionId").GetGuid().Should().Be(versionId);
        await using var services = customerApp.Services.CreateAsyncScope();
        var reader = services.ServiceProvider.GetRequiredService<ICustomerAgreementSelectionReader>();
        var id = TradingRelationshipId.FromGuid(relationshipId);
        (await reader.GetActiveVersionIdAsync(id)).Should().Be(new AgreementVersionId(versionId));
        await ChangeSelection(customer, url, versionId, null);
        (await reader.GetActiveVersionIdAsync(id)).Should().BeNull();
        (await reader.GetActiveVersionIdAsync(id, asOfUtc: null, throughSequence: 1))
            .Should().Be(new AgreementVersionId(versionId));
        (await reader.GetHistoryAsync(id, pageSize: 1)).Items.Single().ToVersionId
            .Should().Be(new AgreementVersionId(versionId));
        (await supplier.GetFromJsonAsync<JsonElement>($"{url}?throughSequence=1"))
            .GetProperty("activeVersionId").GetGuid().Should().Be(versionId);
        var page = await supplier.GetFromJsonAsync<JsonElement>($"{url}/history?pageSize=1");
        var transition = page.GetProperty("items").EnumerateArray().Single();
        transition.GetProperty("sequence").GetInt64().Should().Be(1);
        transition.GetProperty("fromVersionId").ValueKind.Should().Be(JsonValueKind.Null);
        transition.GetProperty("toVersionId").GetGuid().Should().Be(versionId);
        transition.GetProperty("personId").GetGuid().Should().Be(owner.Value);
        page.GetProperty("nextAfterSequence").GetInt64().Should().Be(1);
        var next = await supplier.GetFromJsonAsync<JsonElement>($"{url}/history?afterSequence=1");
        next.GetProperty("items").EnumerateArray().Single().GetProperty("toVersionId").ValueKind.Should().Be(JsonValueKind.Null);
        next.GetProperty("nextAfterSequence").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("?asOfUtc=2026-11-27T12:00:00", "asOfUtc")]
    [InlineData("?asOfUtc=2026-11-27T12:00:00Z&throughSequence=1", "throughSequence")]
    [InlineData("?throughSequence=-1", "throughSequence")]
    [InlineData("/history?afterSequence=-1", "afterSequence")]
    [InlineData("/history?pageSize=101", "pageSize")]
    public async Task Selection_read_endpoints_reject_invalid_query_parameters(string suffix, string parameter)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var version = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        using var response = await supplier.GetAsync(SelectionUrl(version.RelationshipId) + suffix);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty(parameter, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Selection_requires_both_nullable_fields_and_reports_stale_requests()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var publication = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        publication.StatusCode.Should().Be(HttpStatusCode.Created);
        var published = await publication.Content.ReadFromJsonAsync<JsonElement>();
        var versionId = published.GetProperty("id").GetGuid();
        var url = $"/api/v1/trading-relationships/{published.GetProperty("tradingRelationshipId").GetGuid()}/agreement-selection";
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);

        using var missingFrom = await customer.PutAsJsonAsync(url, new { toVersionId = versionId });
        missingFrom.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missingFrom.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("fromVersionId", out _).Should().BeTrue();
        using var missingTo = await customer.PutAsJsonAsync(url, new { fromVersionId = (Guid?)null });
        missingTo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missingTo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("toVersionId", out _).Should().BeTrue();
        using var noSelection = await customer.PutAsJsonAsync(url, new { fromVersionId = (Guid?)null, toVersionId = (Guid?)null });
        noSelection.StatusCode.Should().Be(HttpStatusCode.OK);
        using var activated = await customer.PutAsJsonAsync(url, new { fromVersionId = (Guid?)null, toVersionId = versionId });
        activated.StatusCode.Should().Be(HttpStatusCode.OK);
        using var stale = await customer.PutAsJsonAsync(url, new { fromVersionId = (Guid?)null, toVersionId = (Guid?)null });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("agreement_selection_conflict");
        using var invalidFrom = await customer.PutAsJsonAsync(url, new { fromVersionId = Guid.NewGuid(), toVersionId = versionId });
        invalidFrom.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Selection_checks_authority_and_ended_status_even_for_no_ops_but_allows_deactivation()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var terms = Publication(scope.List);
        terms["validFrom"] = "1900-01-01";
        terms["validTo"] = "1900-12-31";
        using var publication = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", terms);
        publication.StatusCode.Should().Be(HttpStatusCode.Created);
        using var withdrawn = await supplier.PostAsJsonAsync($"{publication.Headers.Location}/withdraw", new { reason = "Superseded" });
        withdrawn.StatusCode.Should().Be(HttpStatusCode.OK);
        var published = await publication.Content.ReadFromJsonAsync<JsonElement>();
        var versionId = published.GetProperty("id").GetGuid();
        var url = $"/api/v1/trading-relationships/{published.GetProperty("tradingRelationshipId").GetGuid()}/agreement-selection";
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        var member = await AddAcceptanceActor(scope.Customer, OrganizationRole.Member);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        await using var memberApp = new ApiApplication(fixture.ConnectionString, member);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        using var customerMember = AcceptanceClient(memberApp, scope.Customer);
        var activate = new { fromVersionId = (Guid?)null, toVersionId = versionId };

        using var hidden = await supplier.PutAsJsonAsync(url, activate);
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var activated = await customer.PutAsJsonAsync(url, activate);
        activated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await activated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("warnings").EnumerateArray()
            .Select(warning => warning.GetString()).Should().BeEquivalentTo(
                new[] { "agreement_version_withdrawn", "agreement_version_outside_validity" });
        using var forbidden = await customerMember.PutAsJsonAsync(url, activate);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using (var db = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions()))
        {
            (await db.TradingRelationships.SingleAsync()).End(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        using var ended = await customer.PutAsJsonAsync(url, activate);
        ended.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ended.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("agreement_relationship_ended");
        (await customerMember.GetFromJsonAsync<JsonElement>(url)).GetProperty("activeVersionId").GetGuid().Should().Be(versionId);
        using var deactivated = await customer.PutAsJsonAsync(url, new { fromVersionId = versionId, toVersionId = (Guid?)null });
        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deactivated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activeVersionId").ValueKind
            .Should().Be(JsonValueKind.Null);
        (await customerMember.GetFromJsonAsync<JsonElement>($"{url}?throughSequence=1"))
            .GetProperty("activeVersionId").GetGuid().Should().Be(versionId);
        (await customerMember.GetFromJsonAsync<JsonElement>($"{url}/history"))
            .GetProperty("items").GetArrayLength().Should().Be(2);

        var supplierId = new OrganizationId(Guid.Parse(supplier.DefaultRequestHeaders.GetValues("organization_id").Single()));
        var supplierMemberId = await AddAcceptanceActor(supplierId, OrganizationRole.Member);
        await using var supplierMemberApp = new ApiApplication(fixture.ConnectionString, supplierMemberId);
        using var supplierMember = AcceptanceClient(supplierMemberApp, supplierId);
        (await supplierMember.GetFromJsonAsync<JsonElement>($"{url}?throughSequence=1"))
            .GetProperty("activeVersionId").GetGuid().Should().Be(versionId);
        (await supplierMember.GetFromJsonAsync<JsonElement>($"{url}/history"))
            .GetProperty("items").GetArrayLength().Should().Be(2);
    }
}
