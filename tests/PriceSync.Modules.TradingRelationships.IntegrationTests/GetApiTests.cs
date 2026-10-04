using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Features.Get;
using PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;
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
