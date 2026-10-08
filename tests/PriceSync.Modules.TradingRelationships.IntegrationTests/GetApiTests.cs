using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    private readonly FrozenTimeProvider _time = new(DateTimeOffset.UtcNow);
    public Task DisposeAsync() => Task.CompletedTask;
    public Task InitializeAsync() => fixture.ResetAsync();

    [Fact]
    public async Task Get_ShouldReturnBadRequest_WhenGuidIsEmpty()
    {
        var application = CreateApplication();
        var client = application.CreateClient();

        var response = await client.GetAsync(Url(Guid.Empty));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        content.GetProperty("code").GetString().Should().Be(GetErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Get_ShouldReturnBadRequest_WhenOrganizationIdIsMissing()
    {
        var application = CreateApplication(authenticated: true);
        var client = application.CreateClient();

        var response = await client.GetAsync(Url(Guid.NewGuid()));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        content.GetProperty("code").GetString().Should().Be(OrganizationContextErrors.Required);
    }

    [Fact]
    public async Task Get_ShouldReturnForbidden_WhenUserLacksCapabilityAndIsPartOfTradingRelationship()
    {
        var application = CreateApplication(allowed: false);
        var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationShipAsync(_time.GetUtcNow());
        client.DefaultRequestHeaders.Add("organization_id", tradingRelationship.CustomerOrganizationId.ToString());

        var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        content.GetProperty("code").GetString().Should().Be(GetErrorCodes.Forbidden);
    }
    [Fact]
    public async Task Get_ShouldReturnBadRequest_WhenOrganizationStatusIsNotFound()
    {
        var application = CreateApplication(status: _ => OrganizationStatusResult.NotFound);
        var client = application.CreateClient();
        var organizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("organization_id", organizationId.ToString());
        var tradingRelationship = await fixture.CreateTradingRelationShipAsync(_time.GetUtcNow());

        var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        content.GetProperty("code").GetString().Should().Be(OrganizationContextErrors.Required);
    }
    [Theory]
    [InlineData(OrganizationStatusResult.Archived)]
    [InlineData(OrganizationStatusResult.Active)]
    public async Task Get_ShouldReturnOk_ForArchivedAndActiveStatus(OrganizationStatusResult statusResult)
    {
        var application = CreateApplication(status: _ => statusResult);
        var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationShipAsync(_time.GetUtcNow());
        client.DefaultRequestHeaders.Add("organization_id", tradingRelationship.CustomerOrganizationId.ToString());

        var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
    [Fact]
    public async Task Get_ShouldReturnUnauthorized_WhenNotAuthenticated()
    {
        var application = CreateApplication(authenticated: false);
        var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationShipAsync(_time.GetUtcNow());

        var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        content.GetProperty("code").GetString().Should().Be("authentication_required");
    }
    [Fact]
    public async Task Get_ShouldReturnTradingRelationshipResponseModelIfOrganizationIsCuromter_OnSuccess()
    {
        var application = CreateApplication();
        var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationShipAsync(_time.GetUtcNow());
        client.DefaultRequestHeaders.Add("organization_id", tradingRelationship.CustomerOrganizationId.ToString());

        var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<TradingRelationshipResponse>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        content.Should().NotBeNull();
        content.Id.Should().Be(tradingRelationship.Id.Value);
        content.SupplierId.Should().Be(tradingRelationship.SupplierOrganizationId.Value);
        content.CustomerId.Should().Be(tradingRelationship.CustomerOrganizationId.Value);
        content.Status.Should().Be("active");
    }
    [Fact]
    public async Task Get_ShouldReturnTradingRelationshipResponseModelIfOrganizationIsSupplier_OnSuccess()
    {
        var application = CreateApplication();
        var client = application.CreateClient();
        var tradingRelationship = await fixture.CreateTradingRelationShipAsync(_time.GetUtcNow());
        client.DefaultRequestHeaders.Add("organization_id", tradingRelationship.SupplierOrganizationId.ToString());

        var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<TradingRelationshipResponse>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        content.Should().NotBeNull();
        content.Id.Should().Be(tradingRelationship.Id.Value);
        content.SupplierId.Should().Be(tradingRelationship.SupplierOrganizationId.Value);
        content.CustomerId.Should().Be(tradingRelationship.CustomerOrganizationId.Value);
        content.Status.Should().Be("active");
    }
    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenTradingRelationshipNotFound()
    {
        var application = CreateApplication();
        var client = application.CreateClient();
        var organizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("organization_id", organizationId.ToString());
        var nonExistentId = Guid.NewGuid();

        var response = await client.GetAsync(Url(nonExistentId));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        content.GetProperty("code").GetString().Should().Be(GetErrorCodes.NotFound);
    }
    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenTRIsFoundButOrganizationIsNotPartOfIt()
    {
        var application = CreateApplication(allowed: false);
        var client = application.CreateClient();
        var otherOrganizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("organization_id", otherOrganizationId.ToString());
        var tradingRelationship = await fixture.CreateTradingRelationShipAsync(_time.GetUtcNow());
        
        var response = await client.GetAsync(Url(tradingRelationship.Id.Value));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        content.GetProperty("code").GetString().Should().Be(GetErrorCodes.NotFound);
    }

    private ApiApplication CreateApplication(bool allowed = true, bool authenticated = true,
        Func<OrganizationId, OrganizationStatusResult>? status = null,
        IReadOnlyCollection<IInterceptor>? interceptors = null, TimeProvider? time = null) => new(
        fixture.ConnectionString,
        authenticated ? new PersonId(Guid.CreateVersion7()) : null,
        new StubOrganizationAuthorizer(allowed), new StubOrganizationStatusReader(status ?? (x => OrganizationStatusResult.Active)), time ?? _time, interceptors);
    private string Url(Guid id) => $"/api/v1/trading-relationships/{id}";
    private async Task<JsonElement> GetByIdAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync(Url(id));
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

}
