using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Theory]
    [InlineData(1, HttpStatusCode.OK)]
    [InlineData(2000, HttpStatusCode.OK)]
    [InlineData(2001, HttpStatusCode.BadRequest)]
    public async Task Withdrawal_validates_reason_length_after_trimming(int length, HttpStatusCode status)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var version = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var reason = new string('x', length);
        using var response = await supplier.PostAsJsonAsync($"{version.Location}/withdraw", new { reason = $"  {reason}  " });

        response.StatusCode.Should().Be(status);
        var read = await supplier.GetFromJsonAsync<JsonElement>(version.Location);
        var withdrawal = read.GetProperty("withdrawal");
        if (status == HttpStatusCode.OK)
            withdrawal.GetProperty("reason").GetString().Should().Be(reason);
        else
            withdrawal.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Withdrawal_requires_an_owner_of_an_active_supplier()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var version = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var supplierId = new OrganizationId(Guid.Parse(supplier.DefaultRequestHeaders.GetValues("organization_id").Single()));
        var member = await AddAcceptanceActor(supplierId, OrganizationRole.Member);
        await using var memberApp = new ApiApplication(fixture.ConnectionString, member);
        using var memberClient = AcceptanceClient(memberApp, supplierId);
        var request = new { reason = "Incorrect terms" };
        var url = $"{version.Location}/withdraw";

        using var memberResponse = await memberClient.PostAsJsonAsync(url, request);
        memberResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using (var db = new OrganizationAccessDbContext(fixture.CreateOptions()))
        {
            (await db.Organizations.SingleAsync(x => x.Id == supplierId)).Archive(TimeProvider.System);
            await db.SaveChangesAsync();
        }
        using var archivedResponse = await supplier.PostAsJsonAsync(url, request);
        archivedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using var agreements = SelectionContext();
        (await agreements.Withdrawals.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_withdrawals_preserve_the_winner(bool sameReason)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var publisher = scope.Client;
        var gate = new WithdrawalInsertGate();
        var supplierId = new OrganizationId(Guid.Parse(publisher.DefaultRequestHeaders.GetValues("organization_id").Single()));
        var firstOwner = await AddAcceptanceActor(supplierId, OrganizationRole.Owner);
        var secondOwner = await AddAcceptanceActor(supplierId, OrganizationRole.Owner);
        var firstTime = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        var secondTime = firstTime.AddMinutes(1);
        await using var firstApp = WithdrawalApi(firstOwner, firstTime);
        await using var secondApp = WithdrawalApi(secondOwner, secondTime);
        using var first = AcceptanceClient(firstApp, supplierId);
        using var second = AcceptanceClient(secondApp, supplierId);
        var version = await PublishSelectionTerms(publisher, scope.Agreement, scope.List);
        var url = $"{version.Location}/withdraw";
        var results = await Task.WhenAll(
            first.PostAsJsonAsync(url, new { reason = " first " }),
            second.PostAsJsonAsync(url, new { reason = sameReason ? "first" : "second" }));

        ApiApplication WithdrawalApi(PersonId owner, DateTimeOffset now) =>
            new(fixture.ConnectionString, owner, services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new ListTestClock(now));
                services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate));
            });
        try
        {
            results.Count(response => response.IsSuccessStatusCode).Should().Be(sameReason ? 2 : 1);
            if (!sameReason)
                results.Single(response => !response.IsSuccessStatusCode).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var winner = await results.First(response => response.IsSuccessStatusCode).Content.ReadFromJsonAsync<JsonElement>();
            var reason = winner.GetProperty("reason").GetString();
            await using var db = SelectionContext();
            var stored = (await db.Withdrawals.ToListAsync()).Should().ContainSingle().Which;
            new[] { firstOwner, secondOwner }.Should().Contain(stored.WithdrawnByPersonId);
            stored.WithdrawnAt.Should().Be(stored.WithdrawnByPersonId == firstOwner ? firstTime : secondTime);
            winner.GetProperty("withdrawnByPersonId").GetGuid().Should().Be(stored.WithdrawnByPersonId.Value);
            winner.GetProperty("withdrawnAt").GetDateTimeOffset().Should().Be(stored.WithdrawnAt);
            stored.Reason.Should().Be(reason);

            var losingClient = stored.WithdrawnByPersonId == firstOwner ? second : first;
            using var retry = await losingClient.PostAsJsonAsync(url, new { reason });
            retry.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement.DeepEquals(winner, await retry.Content.ReadFromJsonAsync<JsonElement>()).Should().BeTrue();
            using var conflict = await losingClient.PostAsJsonAsync(url, new { reason = "different" });
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
