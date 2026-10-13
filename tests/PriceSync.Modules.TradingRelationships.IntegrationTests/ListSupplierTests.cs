using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;
using System.Net.Http.Json;
using System.Text.Json;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[Collection(TradingRelationshipsPostgreSqlCollection.Name)]
public class ListSupplierTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);
    public Task DisposeAsync() => Task.CompletedTask;
    public Task InitializeAsync() => fixture.ResetAsync();

    [Fact]
    public async Task ListSupplier_ShouldReturnBadRequest_ForInvalidCursor()
    {
        // Arrange
        var application = CreateApplication();
        var client = application.CreateClient();
        var invalidCursor = "invalid_cursor";

        // Act
        var result = await client.GetAsync(Url(cursor: invalidCursor));
        var jsonContent = await result.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        jsonContent.GetProperty("code").GetString().Should().Be("invalid_cursor");
    }

    private string Url(string? status = null, string? cursor = null)
    {
        var queryParams = new List<string>();
        if (status is not null) queryParams.Add($"status={status}");
        if (cursor is not null) queryParams.Add($"cursor={cursor}");
        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        return $"/api/v1/trading-relationships/supplier{queryString}";
    }
    private ApiApplication CreateApplication(
        IOrganizationAuthorizer? authorizer = null,
        IOrganizationStatusReader? statusReader = null,
        PersonId? personId = null,
        IReadOnlyCollection<IInterceptor>? interceptors = null)
    {
        return new ApiApplication(
            fixture.ConnectionString,
            personId ?? new PersonId(Guid.CreateVersion7()),
            authorizer ?? new StubOrganizationAuthorizer(allowed: true),
            statusReader ?? new StubOrganizationStatusReader(x => OrganizationStatusResult.Active),
            new FrozenTimeProvider(CreatedAt),
            interceptors);
    }
}
