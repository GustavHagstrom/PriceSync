using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using PriceSync.Modules.PricePublication.Contracts;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace PriceSync.Modules.PricePublication.IntegrationTests;

[Collection(PricePublicationPostgreSqlCollection.Name)]
public sealed class ScopeVerificationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Complete_set_is_verified_from_real_persistence_without_published_versions()
    {
        var (application, supplier, customer) = await CreateApplication();
        await using var app = application;
        var general = await CreateList(app, supplier, "general");
        var net = await CreateList(app, supplier, "customerSpecificNet", customer);
        using var scope = app.Services.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IPriceListScopeVerifier>();
        var result = await verifier.VerifyAsync(supplier, customer,
            [new(general, PriceListType.General), new(net, PriceListType.CustomerSpecificNet)]);
        var verified = result.Should().BeOfType<PriceListVerificationResult.Verified>().Subject;
        verified.Scopes.Should().BeEquivalentTo(new[]
        {
            new VerifiedPriceListScope(general, supplier, PriceListType.General, "SEK", "Sweden", null),
            new VerifiedPriceListScope(net, supplier, PriceListType.CustomerSpecificNet, "SEK", "Sweden", customer),
        });
    }

    [Fact]
    public async Task Invalid_complete_set_returns_every_bad_reference_without_partial_verified_scopes()
    {
        var (application, supplier, customer) = await CreateApplication();
        await using var app = application;
        var valid = await CreateList(app, supplier, "general");
        var incompatible = await CreateList(app, supplier, "general");
        var wrongCustomer = await CreateList(app, supplier, "customerSpecificNet", supplier);
        // A genuine list owned by another Supplier, created through the same public API.
        await using (var db = new OrganizationAccessDbContext(fixture.CreateOptions()))
        {
            var membership = await db.OrganizationMemberships.FirstAsync();
            db.OrganizationMemberships.Add(OrganizationMembership.Create(new(Guid.CreateVersion7()), customer,
                membership.PersonId, [OrganizationRole.Owner], TimeProvider.System));
            await db.SaveChangesAsync();
        }
        var wrongSupplier = await CreateList(app, customer, "general");
        var missing = PriceListId.Create();
        using var scope = app.Services.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IPriceListScopeVerifier>();
        var result = await verifier.VerifyAsync(supplier, customer,
        [
            new(valid, PriceListType.General), new(missing, PriceListType.General),
            new(incompatible, PriceListType.CustomerSpecificNet), new(wrongSupplier, PriceListType.General),
            new(wrongCustomer, PriceListType.CustomerSpecificNet), new(default, PriceListType.General),
        ]);
        result.Should().BeOfType<PriceListVerificationResult.Invalid>().Which.References.Should().BeEquivalentTo(new[]
        {
            new InvalidPriceListReference(missing, InvalidPriceListReferenceReason.NotFound),
            new InvalidPriceListReference(incompatible, InvalidPriceListReferenceReason.IncompatibleType),
            new InvalidPriceListReference(wrongSupplier, InvalidPriceListReferenceReason.WrongSupplier),
            new InvalidPriceListReference(wrongCustomer, InvalidPriceListReferenceReason.WrongCustomer),
            new InvalidPriceListReference(default, InvalidPriceListReferenceReason.InvalidId),
        });
    }

    [Fact]
    public async Task Database_lookup_failure_is_retryable_unavailable_and_recovers_without_partial_success()
    {
        var (application, supplier, customer) = await CreateApplication();
        await using var app = application;
        var valid = await CreateList(app, supplier, "general");
        using var scope = app.Services.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IPriceListScopeVerifier>();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var hide = new NpgsqlCommand("alter table price_publication.price_lists rename to unavailable_price_lists", connection);
        await hide.ExecuteNonQueryAsync();
        try
        {
            var result = await verifier.VerifyAsync(supplier, customer,
                [new(valid, PriceListType.General), new(PriceListId.Create(), PriceListType.General)]);
            result.Should().BeOfType<PriceListVerificationResult.Unavailable>().Which.Retryable.Should().BeTrue();
            using var client = app.CreateClient();
            client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
            (await client.GetAsync($"/api/v1/price-lists/{valid}")).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await using var restore = new NpgsqlCommand("alter table price_publication.unavailable_price_lists rename to price_lists", connection);
            await restore.ExecuteNonQueryAsync();
        }
        (await verifier.VerifyAsync(supplier, customer, [new(valid, PriceListType.General)]))
            .Should().BeOfType<PriceListVerificationResult.Verified>();
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_instead_of_becoming_an_unavailable_result()
    {
        var (application, supplier, customer) = await CreateApplication();
        await using var app = application;
        using var scope = app.Services.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IPriceListScopeVerifier>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var verify = () => verifier.VerifyAsync(supplier, customer,
            [new(PriceListId.Create(), PriceListType.General)], cancellation.Token);
        await verify.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Connection_failure_is_also_explicitly_retryable_unavailable()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PricePublication"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1;Pooling=false",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPricePublicationModule(configuration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IPriceListScopeVerifier>();
        var result = await verifier.VerifyAsync(OrganizationId.Create(), OrganizationId.Create(),
            [new(PriceListId.Create(), PriceListType.General)]);
        result.Should().BeOfType<PriceListVerificationResult.Unavailable>().Which.Retryable.Should().BeTrue();
    }

    private async Task<(ApiApplication, OrganizationId, OrganizationId)> CreateApplication()
    {
        var person = new PersonId(Guid.CreateVersion7());
        var supplier = Organization.Create(OrganizationId.Create(), "Supplier",
            LegalOrganizationIdentity.Create("SE", "5560160680"), TimeProvider.System);
        var customer = Organization.Create(OrganizationId.Create(), "Customer",
            LegalOrganizationIdentity.Create("SE", "5560360793"), TimeProvider.System);
        await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
        db.Organizations.AddRange(supplier, customer);
        db.OrganizationMemberships.Add(OrganizationMembership.Create(new(Guid.CreateVersion7()), supplier.Id, person,
            [OrganizationRole.Owner], TimeProvider.System));
        await db.SaveChangesAsync();
        return (new ApiApplication(fixture.ConnectionString, person), supplier.Id, customer.Id);
    }

    private static async Task<PriceListId> CreateList(ApiApplication app, OrganizationId supplier, string type,
        OrganizationId? customer = null)
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var response = await client.PostAsJsonAsync("/api/v1/price-lists", new
        {
            name = "List", type, currency = "SEK", market = "Sweden", customerOrganizationId = customer?.Value,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new PriceListId(body.GetProperty("id").GetGuid());
    }
}
