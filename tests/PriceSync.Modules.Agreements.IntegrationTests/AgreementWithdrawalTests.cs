using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_withdrawals_preserve_the_winner(bool sameReason)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var publisher = scope.Client;
        var gate = new WithdrawalInsertGate();
        await using var guarded = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate))));
        using var supplier = guarded.CreateClient();
        supplier.DefaultRequestHeaders.Add("organization_id", publisher.DefaultRequestHeaders.GetValues("organization_id"));
        using var publication = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        publication.StatusCode.Should().Be(HttpStatusCode.Created);
        var url = publication.Headers.Location!;
        var results = await Task.WhenAll(
            supplier.PostAsJsonAsync($"{url}/withdraw", new { reason = " first " }),
            supplier.PostAsJsonAsync($"{url}/withdraw", new { reason = sameReason ? "first" : "second" }));
        try
        {
            results.Count(response => response.IsSuccessStatusCode).Should().Be(sameReason ? 2 : 1);
            if (!sameReason)
                results.Single(response => !response.IsSuccessStatusCode).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var winner = await results.First(response => response.IsSuccessStatusCode).Content.ReadFromJsonAsync<JsonElement>();
            var reason = winner.GetProperty("reason").GetString();
            using var retry = await supplier.PostAsJsonAsync($"{url}/withdraw", new { reason });
            retry.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement.DeepEquals(winner, await retry.Content.ReadFromJsonAsync<JsonElement>()).Should().BeTrue();
            using var conflict = await supplier.PostAsJsonAsync($"{url}/withdraw", new { reason = "different" });
            conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var result in results) result.Dispose();
        }
    }

    [Fact]
    public async Task Withdrawal_preserves_published_terms_and_exposes_normalized_reason()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var publication = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        publication.StatusCode.Should().Be(HttpStatusCode.Created);
        var original = await publication.Content.ReadFromJsonAsync<JsonElement>();
        var url = publication.Headers.Location!;
        var actor = new PersonId(Guid.CreateVersion7());
        var unrelatedOrganization = await SeedOrganization(actor, "5560004615");
        await using var unrelatedApp = new ApiApplication(fixture.ConnectionString, actor);
        using var unrelatedOwner = unrelatedApp.CreateClient();
        unrelatedOwner.DefaultRequestHeaders.Add("organization_id", unrelatedOrganization.ToString());
        var request = new { reason = "Incorrect terms" };
        using var denied = await unrelatedOwner.PostAsJsonAsync($"{url}/withdraw", request);
        using var unknown = await unrelatedOwner.PostAsJsonAsync(
            $"{scope.Agreement}/versions/{Guid.CreateVersion7()}/withdraw", request);
        denied.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemWithoutTrace(denied)).Should().Be(await ProblemWithoutTrace(unknown));
        using var invalid = await supplier.PostAsJsonAsync($"{url}/withdraw", new { reason = "   " });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using (var db = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions()))
        {
            (await db.TradingRelationships.SingleAsync()).End(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        using var withdrawal = await supplier.PostAsJsonAsync($"{url}/withdraw", new { reason = "  Incorrect terms  " });
        withdrawal.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = await supplier.GetFromJsonAsync<JsonElement>(url);
        read.GetProperty("withdrawal").GetProperty("reason").GetString().Should().Be("Incorrect terms");
        read.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString())
            .Should().Contain("agreement_version_withdrawn");
        foreach (var term in new[] { "coverage", "rounding", "groupDiscounts", "validFrom", "validTo", "publishedAt" })
            JsonElement.DeepEquals(read.GetProperty(term), original.GetProperty(term)).Should().BeTrue();
        await using var consumer = app.Services.CreateAsyncScope();
        var facts = await consumer.ServiceProvider.GetRequiredService<IAgreementVersionReader>()
            .GetAsync(new AgreementVersionId(read.GetProperty("id").GetGuid()));
        facts!.Withdrawal!.Reason.Should().Be("Incorrect terms");
        facts.IsOutsideValidityOn(facts.ValidFrom.AddDays(-1)).Should().BeTrue();
        facts.IsOutsideValidityOn(facts.ValidFrom).Should().BeFalse();
    }

    private sealed class WithdrawalInsertGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _bothInserting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AgreementVersionWithdrawal>().Any(entry => entry.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _bothInserting.TrySetResult();
                await _bothInserting.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
