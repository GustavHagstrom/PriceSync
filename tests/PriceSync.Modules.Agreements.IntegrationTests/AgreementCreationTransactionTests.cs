using System.Data;
using System.Data.Common;
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_matching_creation_waits_for_commit_or_takes_over_after_rollback(bool failWinner)
    {
        var scope = await SeedAgreementScope();
        var gate = new CommitGate(failWinner);
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner,
            services => services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate)));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var winner = PostAgreement(client, scope.Relationship.Value, "concurrent");
        Task<HttpResponseMessage> waiter;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            gate.Isolation.Should().Be(IsolationLevel.ReadCommitted);
            waiter = PostAgreement(client, scope.Relationship.Value, "concurrent");
            await WaitForBlockedReservation();
            waiter.IsCompleted.Should().BeFalse("a matching request must wait for the uncommitted reservation");
        }
        finally
        {
            gate.Release.TrySetResult();
        }
        var original = await winner;
        var second = await waiter;
        original.StatusCode.Should().Be(failWinner ? HttpStatusCode.InternalServerError : HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        if (!failWinner)
        {
            original.Headers.Location.Should().Be(second.Headers.Location);
            (await original.Content.ReadAsStringAsync()).Should().Be(await second.Content.ReadAsStringAsync());
        }
        var replay = await PostAgreement(client, scope.Relationship.Value, "concurrent");
        replay.Headers.Location.Should().Be(second.Headers.Location);
        (await replay.Content.ReadAsStringAsync()).Should().Be(await second.Content.ReadAsStringAsync());
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.PriceAgreements.CountAsync()).Should().Be(1);
        var record = await db.CreationReplays.SingleAsync();
        record.ResponseStatus.Should().Be(201);
        record.ResponseBody.Should().Be(await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Failed_commit_rolls_back_both_agreement_and_reservation_and_the_key_can_be_retried()
    {
        var scope = await SeedAgreementScope();
        var gate = new CommitGate(failWinner: true);
        gate.Release.SetResult();
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner,
            services => services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate)));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());

        (await PostAgreement(client, scope.Relationship.Value, "rollback")).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using (var db = new AgreementsDbContext(fixture.CreateAgreementOptions()))
        {
            (await db.PriceAgreements.CountAsync()).Should().Be(0);
            (await db.CreationReplays.CountAsync()).Should().Be(0);
        }
        (await PostAgreement(client, scope.Relationship.Value, "rollback")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task WaitForBlockedReservation()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                AND query LIKE '%INSERT INTO agreements.agreement_creation_replays%')
            """, connection);
        while (!(bool)(await command.ExecuteScalarAsync(timeout.Token))!)
            await Task.Delay(20, timeout.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Creation_and_replay_use_the_relationship_status_observed_before_the_local_transaction(bool replay)
    {
        var scope = await SeedAgreementScope();
        HttpResponseMessage? original = null;
        if (replay)
        {
            await using var initialApp = new ApiApplication(fixture.ConnectionString, scope.Owner);
            using var initialClient = initialApp.CreateClient();
            initialClient.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
            original = await PostAgreement(initialClient, scope.Relationship.Value, "point-in-time");
            original.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        var interceptor = new EndRelationshipOnReservation(async () =>
        {
            await using var relationships = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions());
            (await relationships.TradingRelationships.FindAsync(scope.Relationship))!.End(DateTimeOffset.UtcNow);
            await relationships.SaveChangesAsync();
        });
        await using var app = new ApiApplication(fixture.ConnectionString, scope.Owner,
            services => services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(interceptor)));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", scope.Supplier.ToString());
        var accepted = await PostAgreement(client, scope.Relationship.Value, "point-in-time");
        accepted.StatusCode.Should().Be(HttpStatusCode.Created);
        if (original is not null)
            (await accepted.Content.ReadAsStringAsync()).Should().Be(await original.Content.ReadAsStringAsync());
        (await PostAgreement(client, scope.Relationship.Value, "point-in-time")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.PriceAgreements.CountAsync()).Should().Be(1);
        (await db.CreationReplays.CountAsync()).Should().Be(1);
    }

    private sealed class EndRelationshipOnReservation(Func<Task> endRelationship) : DbCommandInterceptor
    {
        private int _reservations;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO agreements.agreement_creation_replays", StringComparison.Ordinal)
                && Interlocked.Increment(ref _reservations) == 1)
                await endRelationship();
            return result;
        }
    }

    private sealed class CommitGate(bool failWinner) : DbTransactionInterceptor
    {
        private int _commits;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal IsolationLevel Isolation { get; private set; }

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _commits) != 1) return result;
            Isolation = transaction.IsolationLevel;
            Entered.SetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            if (failWinner) throw new InvalidOperationException("Injected failure immediately before commit.");
            return result;
        }
    }
}
