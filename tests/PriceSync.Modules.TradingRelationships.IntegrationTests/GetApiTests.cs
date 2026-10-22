using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Features.Get;
using PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;
using PriceSync.Modules.TradingRelationships.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[Collection(TradingRelationshipsPostgreSqlCollection.Name)]
public sealed class GetApiTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private readonly FrozenTimeProvider _time = new(new DateTimeOffset(2026, 10, 8, 8, 30, 0, TimeSpan.Zero));
    public Task DisposeAsync() => Task.CompletedTask;
    public Task InitializeAsync() => fixture.ResetAsync();

    [Fact]
    public async Task Get_ShouldReturnBadRequest_WhenGuidIsEmpty()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();

        using var response = await client.GetAsync(Url(Guid.Empty));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        content.GetProperty("code").GetString().Should().Be(GetErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Get_ShouldReturnBadRequest_WhenOrganizationIdIsMissing()
    {
        await using var application = CreateApplication(authenticated: true);
        using var client = application.CreateClient();

        using var response = await client.GetAsync(Url(Guid.NewGuid()));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        content.GetProperty("code").GetString().Should().Be(OrganizationContextErrors.Required);
    }

    [Fact]
    public async Task Get_ShouldReturnForbidden_WhenUserLacksCapabilityAndIsPartOfTradingRelationship()
    {
        await using var application = CreateApplication(allowed: false);
        using var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationshipAsync(_time.GetUtcNow());
        client.DefaultRequestHeaders.Add("organization_id", tradingRelationship.CustomerOrganizationId.ToString());

        using var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        content.GetProperty("code").GetString().Should().Be(GetErrorCodes.Forbidden);
    }
    [Fact]
    public async Task Get_ShouldReturnForbidden_ForBothExistingAndUnknownId_WhenUserLacksReadCapability()
    {
        await using var application = CreateApplication(allowed: false);
        using var client = application.CreateClient();

        var relationship = await fixture.CreateTradingRelationshipAsync(_time.GetUtcNow());
        client.DefaultRequestHeaders.Add(
            "organization_id",
            relationship.CustomerOrganizationId.ToString());

        using var existingResponse = await client.GetAsync(Url(relationship.Id.Value));
        using var unknownResponse = await client.GetAsync(Url(Guid.NewGuid()));

        existingResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        unknownResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task Get_ShouldReturnBadRequest_WhenOrganizationStatusIsNotFound()
    {
        await using var application = CreateApplication(status: _ => OrganizationStatusResult.NotFound);
        using var client = application.CreateClient();
        var organizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("organization_id", organizationId.ToString());
        var tradingRelationship = await fixture.CreateTradingRelationshipAsync(_time.GetUtcNow());

        using var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        content.GetProperty("code").GetString().Should().Be(OrganizationContextErrors.Required);
    }
    [Theory]
    [InlineData(OrganizationStatusResult.Archived)]
    [InlineData(OrganizationStatusResult.Active)]
    public async Task Get_ShouldReturnOk_ForArchivedAndActiveStatus(OrganizationStatusResult statusResult)
    {
        await using var application = CreateApplication(status: _ => statusResult);
        using var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationshipAsync(_time.GetUtcNow());
        client.DefaultRequestHeaders.Add("organization_id", tradingRelationship.CustomerOrganizationId.ToString());

        using var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
    [Fact]
    public async Task Get_ShouldReturnUnauthorized_WhenNotAuthenticated()
    {
        await using var application = CreateApplication(authenticated: false);
        using var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationshipAsync(_time.GetUtcNow());

        using var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        content.GetProperty("code").GetString().Should().Be("authentication_required");
    }
    [Fact]
    public async Task Get_ShouldReturnTradingRelationshipResponseModelIfOrganizationIsCustomer_OnSuccess()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationshipAsync(_time.GetUtcNow());
        client.DefaultRequestHeaders.Add("organization_id", tradingRelationship.CustomerOrganizationId.ToString());

        using var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<TradingRelationshipResponse>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        content.Should().NotBeNull();
        content.Id.Should().Be(tradingRelationship.Id.Value);
        content.SupplierId.Should().Be(tradingRelationship.SupplierOrganizationId.Value);
        content.CustomerId.Should().Be(tradingRelationship.CustomerOrganizationId.Value);
        content.CreatedAt.Should().Be(tradingRelationship.CreatedAt);
        content.EndedAt.Should().BeNull();
        content.Status.Should().Be("active");
    }
    [Fact]
    public async Task Get_ShouldReturnTradingRelationshipResponseModelIfOrganizationIsSupplier_OnSuccess()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationshipAsync(_time.GetUtcNow());
        client.DefaultRequestHeaders.Add("organization_id", tradingRelationship.SupplierOrganizationId.ToString());

        using var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<TradingRelationshipResponse>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        content.Should().NotBeNull();
        content.Id.Should().Be(tradingRelationship.Id.Value);
        content.SupplierId.Should().Be(tradingRelationship.SupplierOrganizationId.Value);
        content.CustomerId.Should().Be(tradingRelationship.CustomerOrganizationId.Value);
        content.CreatedAt.Should().Be(tradingRelationship.CreatedAt);
        content.EndedAt.Should().BeNull();
        content.Status.Should().Be("active");
    }
    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenTradingRelationshipNotFound()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var organizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("organization_id", organizationId.ToString());
        var nonExistentId = Guid.NewGuid();

        using var response = await client.GetAsync(Url(nonExistentId));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        content.GetProperty("code").GetString().Should().Be(GetErrorCodes.NotFound);
    }
    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenTRIsFoundButOrganizationIsNotPartOfIt()
    {
        await using var application = CreateApplication(allowed: true);
        using var client = application.CreateClient();
        var otherOrganizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("organization_id", otherOrganizationId.ToString());
        var tradingRelationship = await fixture.CreateTradingRelationshipAsync(_time.GetUtcNow());
        
        using var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        content.GetProperty("code").GetString().Should().Be(GetErrorCodes.NotFound);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_ShouldCheckReadCapability_ForActingOrganization(bool supplierActs)
    {
        var authorizer = new StubOrganizationAuthorizer();
        await using var application = CreateApplication(authorizer: authorizer);
        using var client = application.CreateClient();
        var relationship = await fixture.CreateTradingRelationshipAsync(_time.GetUtcNow());
        var actingOrganizationId = supplierActs
            ? relationship.SupplierOrganizationId
            : relationship.CustomerOrganizationId;
        client.DefaultRequestHeaders.Add("organization_id", actingOrganizationId.Value.ToString());

        using var response = await client.GetAsync(Url(relationship.Id.Value));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        authorizer.Calls.Should().ContainSingle()
            .Which.Should().Be((actingOrganizationId, OrganizationCapability.ReadTradingRelationships));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_ShouldReturnEndedRelationshipWithHistory_ForEitherParty(bool supplierActs)
    {
        var relationship = TradingRelationship.Create(
            OrganizationId.Create(), OrganizationId.Create(), _time.GetUtcNow());
        var endedAt = _time.GetUtcNow().AddDays(1);
        relationship.End(endedAt);
        await using (var context = new TradingRelationshipsDbContext(fixture.CreateOptions()))
        {
            context.TradingRelationships.Add(relationship);
            await context.SaveChangesAsync();
        }

        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var actingOrganizationId = supplierActs
            ? relationship.SupplierOrganizationId
            : relationship.CustomerOrganizationId;
        client.DefaultRequestHeaders.Add("organization_id", actingOrganizationId.Value.ToString());

        using var response = await client.GetAsync(Url(relationship.Id.Value));
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<TradingRelationshipResponse>();

        content.Should().NotBeNull();
        content.Id.Should().Be(relationship.Id.Value);
        content.SupplierId.Should().Be(relationship.SupplierOrganizationId.Value);
        content.CustomerId.Should().Be(relationship.CustomerOrganizationId.Value);
        content.CreatedAt.Should().Be(relationship.CreatedAt);
        content.EndedAt.Should().Be(endedAt);
        content.Status.Should().Be("ended");
    }

    private ApiApplication CreateApplication(bool allowed = true, bool authenticated = true,
        Func<OrganizationId, OrganizationStatusResult>? status = null,
        IReadOnlyCollection<IInterceptor>? interceptors = null, TimeProvider? time = null, IOrganizationAuthorizer? authorizer = null) => new(
        fixture.ConnectionString,
        authenticated ? new PersonId(Guid.CreateVersion7()) : null,
        authorizer ?? new StubOrganizationAuthorizer(allowed), new StubOrganizationStatusReader(status ?? (x => OrganizationStatusResult.Active)), time ?? _time, interceptors);
    private static string Url(Guid id) => $"/api/v1/trading-relationships/{id}";
}
