using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Features.ListSupplier;
using PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;
using PriceSync.Modules.TradingRelationships.Models;
using PriceSync.Modules.TradingRelationships.Persistence;
using System.Net.Http.Json;
using System.Text.Json;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[Collection(TradingRelationshipsPostgreSqlCollection.Name)]
public class ListSupplierTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);
    private static readonly OrganizationId SupplierId = OrganizationId.Create();
    public Task DisposeAsync() => Task.CompletedTask;
    public Task InitializeAsync() => fixture.ResetAsync();

    [Fact]
    public async Task ListSupplier_ShouldReturnBadRequest_ForInvalidCursor()
    {
        // Arrange
        var application = CreateApplication();
        var client = CreateClient(application);
        var invalidCursor = "invalid_cursor";

        // Act
        var result = await client.GetAsync(Url(cursor: invalidCursor));
        var jsonContent = await result.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        jsonContent.GetProperty("code").GetString().Should().Be("validation_failed");
    }
    [Fact]
    public async Task ListSupplier_ShouldReturnBadRequest_ForInvalidStatus()
    {
        // Arrange
        var application = CreateApplication();
        var client = CreateClient(application);
        var invalidStatus = "invalid_status";
        // Act
        var result = await client.GetAsync(Url(status: invalidStatus));
        var body = await result.Content.ReadAsStringAsync();
        var jsonContent = await result.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        jsonContent.GetProperty("code").GetString().Should().Be("validation_failed");
    }


    [Fact]
    public async Task ListSupplier_ShouldReturnForbidden_ForUnauthorized()
    {
        // Arrange
        var application = CreateApplication(authorizer: new StubOrganizationAuthorizer(allowed: false));
        var client = CreateClient(application);
        var status = "active";

        // Act
        var result = await client.GetAsync(Url(status));
        var jsonContent = await result.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        jsonContent.GetProperty("code").GetString().Should().Be("forbidden");
    }

    [Theory]
    [InlineData(OrganizationStatusResult.Active)]
    [InlineData(OrganizationStatusResult.Archived)]
    [InlineData(OrganizationStatusResult.NotFound)]
    public async Task ListSupplier_ShouldReturnOk_ForAnyOrganizationStatus(OrganizationStatusResult statusResult)
    {
        // Arrange
        var application = CreateApplication(statusReader: new StubOrganizationStatusReader(x => statusResult));
        var client = CreateClient(application);
        var status = "active";

        // Act
        var result = await client.GetAsync(Url(status));
        var jsonContent = await result.Content.ReadFromJsonAsync<JsonElement>();
        // Assert
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
    [Fact]
    public async Task ListSupplier_ShouldReturnNullCursor()
    {
        int numberOfRelationships = 50;
        for(int i = 0; i < numberOfRelationships; i++)
        {
            await fixture.CreateTradingRelationshipAsync(CreatedAt.AddMinutes(i), SupplierId, OrganizationId.Create());
        }
        var application = CreateApplication();
        var client = CreateClient(application);
        
        var result = await client.GetAsync(Url());
        var jsonContent = await result.Content.ReadFromJsonAsync<JsonElement>();

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        jsonContent.GetProperty("tradingRelationships").GetArrayLength().Should().Be(50);
        jsonContent.GetProperty("cursor").ValueKind.Should().Be(JsonValueKind.Null);
    }
    [Fact]
    public async Task ListSupplier_ShouldReturnCursorAbove50()
    {
        int numberOfRelationships = 51;
        var relationships = new List<TradingRelationship>();
        for (int i = 0; i < numberOfRelationships; i++)
        {
            relationships.Add(await fixture.CreateTradingRelationshipAsync(CreatedAt.AddMinutes(i), SupplierId, OrganizationId.Create()));
        }
        relationships.Reverse();
        var application = CreateApplication();
        var client = CreateClient(application);
        var expectedKeyset = ListKeyset.FromTradingRelationship(relationships[49]);

        var result = await client.GetAsync(Url());
        var jsonContent = await result.Content.ReadFromJsonAsync<JsonElement>();

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        jsonContent.GetProperty("tradingRelationships").GetArrayLength().Should().Be(50);
        jsonContent.GetProperty("cursor").GetString().Should().Be(expectedKeyset.Cursor);
    }
    [Fact]
    public async Task ListSupplier_ShouldReturnCorrectRecordsAfterCursor()
    {
        int numberOfRelationships = 10;
        var relationships = new List<TradingRelationship>();
        for (int i = 0; i < numberOfRelationships; i++)
        {
            relationships.Add(await fixture.CreateTradingRelationshipAsync(CreatedAt.AddMinutes(i), SupplierId, OrganizationId.Create()));
        }
        relationships.Reverse();
        var application = CreateApplication();
        var client = CreateClient(application);
        var keyset = ListKeyset.FromTradingRelationship(relationships[4]);

        var result = await client.GetAsync(Url(cursor: keyset.Cursor));
        var response = await result.Content.ReadFromJsonAsync<ListSupplierResponse>();

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Should().NotBeNull();
        response!.TradingRelationships.Select(x => x.Id)
            .Should().Equal(relationships.Skip(5).Select(x => x.Id.Value));
        response.Cursor.Should().BeNull();
    }

    [Fact]
    public async Task ListSupplier_ShouldReturnEveryRecordOnce_WhenFollowingCursorWithEqualCreatedAt()
    {
        // All timestamps match, so the page boundary must also compare IDs.
        var relationships = new List<TradingRelationship>();
        for (var i = 0; i < 51; i++)
        {
            relationships.Add(await fixture.CreateTradingRelationshipAsync(
                CreatedAt, SupplierId, OrganizationId.Create()));
        }

        // Neither another supplier's customers nor our suppliers belong in this list.
        await fixture.CreateTradingRelationshipAsync(CreatedAt, OrganizationId.Create(), OrganizationId.Create());
        await fixture.CreateTradingRelationshipAsync(CreatedAt, OrganizationId.Create(), SupplierId);

        var expectedIds = relationships.OrderByDescending(x => x.Id.Value)
            .Select(x => x.Id.Value).ToArray();
        using var application = CreateApplication();
        using var client = CreateClient(application);

        using var firstResult = await client.GetAsync(Url());
        firstResult.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var firstPage = await firstResult.Content.ReadFromJsonAsync<ListSupplierResponse>();
        firstPage.Should().NotBeNull();
        firstPage!.TradingRelationships.Select(x => x.Id).Should().Equal(expectedIds.Take(50));
        firstPage.Cursor.Should().NotBeNullOrEmpty();

        using var secondResult = await client.GetAsync(Url(cursor: firstPage.Cursor));
        secondResult.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var secondPage = await secondResult.Content.ReadFromJsonAsync<ListSupplierResponse>();
        secondPage.Should().NotBeNull();
        secondPage!.TradingRelationships.Select(x => x.Id).Should().Equal(expectedIds.Skip(50));
        secondPage.Cursor.Should().BeNull();

        firstPage.TradingRelationships.Concat(secondPage.TradingRelationships)
            .Select(x => x.Id).Should().Equal(expectedIds).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ListSupplier_ShouldReturnEmptyListAndNullCursor_WhenNoSuppliersExist()
    {
        using var application = CreateApplication();
        using var client = CreateClient(application);

        using var result = await client.GetAsync(Url());

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var response = await result.Content.ReadFromJsonAsync<ListSupplierResponse>();
        response.Should().NotBeNull();
        response!.TradingRelationships.Should().BeEmpty();
        response.Cursor.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("active")]
    [InlineData("ended")]
    [InlineData("all")]
    public async Task ListSupplier_ShouldFilterStatusAndOrderByCreatedAt(string? status)
    {
        // Insert out of chronological order to make ordering observable.
        var newestActive = await SeedSupplierAsync(CreatedAt.AddMinutes(3));
        var oldestActive = await SeedSupplierAsync(CreatedAt);
        var ended = await SeedSupplierAsync(CreatedAt.AddMinutes(2), ended: true);
        var expected = status switch
        {
            "ended" => [ended],
            "all" => [newestActive, ended, oldestActive],
            _ => new[] { newestActive, oldestActive }
        };
        using var application = CreateApplication();
        using var client = CreateClient(application);

        using var result = await client.GetAsync(Url(status));

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var response = await result.Content.ReadFromJsonAsync<ListSupplierResponse>();
        response.Should().NotBeNull();
        response!.TradingRelationships.Should().Equal(
            expected.Select(x => new TradingRelationshipResponse(x)));
        response.Cursor.Should().BeNull();
    }

    [Theory]
    [InlineData("active")]
    [InlineData("ended")]
    [InlineData("all")]
    public async Task ListSupplier_ShouldPreserveStatusFilter_WhenFollowingCursor(string status)
    {
        var expected = new List<TradingRelationship>();
        for (var i = 0; i < 51; i++)
        {
            expected.Add(await SeedSupplierAsync(
                CreatedAt.AddMinutes(i), ended: status == "ended" || (status == "all" && i % 2 == 0)));
        }

        if (status != "all")
        {
            // Excluded rows on both sides of the page boundary must not affect paging.
            await SeedSupplierAsync(CreatedAt.AddMinutes(100), ended: status == "active");
            await SeedSupplierAsync(CreatedAt.AddSeconds(30), ended: status == "active");
        }

        var expectedIds = expected.OrderByDescending(x => x.CreatedAt).Select(x => x.Id.Value).ToArray();
        using var application = CreateApplication();
        using var client = CreateClient(application);

        using var firstResult = await client.GetAsync(Url(status));
        firstResult.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var firstPage = await firstResult.Content.ReadFromJsonAsync<ListSupplierResponse>();
        firstPage.Should().NotBeNull();
        firstPage!.TradingRelationships.Select(x => x.Id).Should().Equal(expectedIds.Take(50));
        firstPage.Cursor.Should().NotBeNullOrEmpty();

        using var secondResult = await client.GetAsync(Url(status, firstPage.Cursor));
        secondResult.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var secondPage = await secondResult.Content.ReadFromJsonAsync<ListSupplierResponse>();
        secondPage.Should().NotBeNull();
        secondPage!.TradingRelationships.Select(x => x.Id).Should().Equal(expectedIds.Skip(50));
        secondPage.Cursor.Should().BeNull();
    }

    [Fact]
    public async Task ListSupplier_ShouldReturnEmptyList_WhenCursorIsAtLastRecord()
    {
        var relationship = await SeedSupplierAsync(CreatedAt);
        using var application = CreateApplication();
        using var client = CreateClient(application);

        using var result = await client.GetAsync(Url(cursor: ListKeyset.FromTradingRelationship(relationship).Cursor));

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var response = await result.Content.ReadFromJsonAsync<ListSupplierResponse>();
        response.Should().NotBeNull();
        response!.TradingRelationships.Should().BeEmpty();
        response.Cursor.Should().BeNull();
    }

    [Fact]
    public async Task ListSupplier_ShouldReturnBadRequest_WhenActingOrganizationIsMissing()
    {
        using var application = CreateApplication();
        using var client = application.CreateClient();

        using var result = await client.GetAsync(Url());

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var body = await result.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be(OrganizationContextErrors.Required);
    }

    [Fact]
    public async Task ListSupplier_ShouldAuthorizeReadCapability_ForActingOrganization()
    {
        var authorizer = new StubOrganizationAuthorizer(allowed: true);
        using var application = CreateApplication(authorizer: authorizer);
        using var client = CreateClient(application);

        using var result = await client.GetAsync(Url());

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        authorizer.Calls.Should().ContainSingle()
            .Which.Should().Be((SupplierId, OrganizationCapability.ReadTradingRelationships));
    }

    private async Task<TradingRelationship> SeedSupplierAsync(DateTimeOffset createdAt, bool ended = false)
    {
        var relationship = TradingRelationship.Create(SupplierId, OrganizationId.Create(), createdAt);
        if (ended)
        {
            relationship.End(createdAt.AddMinutes(1));
        }

        await using var context = new TradingRelationshipsDbContext(fixture.CreateOptions());
        context.TradingRelationships.Add(relationship);
        await context.SaveChangesAsync();
        return relationship;
    }

    private static HttpClient CreateClient(ApiApplication application)
    {
        var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", SupplierId.Value.ToString());
        return client;
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
