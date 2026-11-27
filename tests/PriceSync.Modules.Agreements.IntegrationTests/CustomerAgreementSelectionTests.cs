using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Concurrent_first_activation_records_one_acceptance_and_transition()
    {
        var versionId = await PublishSelectionVersion();
        var gate = new SelectionCommandGate("INSERT INTO agreements.customer_agreement_states");
        await using var first = SelectionContext(gate);
        await using var second = SelectionContext(gate);
        var relationshipId = (await first.Versions.SingleAsync()).TradingRelationshipId;

        var results = await Task.WhenAll(
            new CustomerAgreementSelectionStore(first, TimeProvider.System).ChangeAsync(
                relationshipId, null, versionId, new PersonId(Guid.NewGuid()), default),
            new CustomerAgreementSelectionStore(second, TimeProvider.System).ChangeAsync(
                relationshipId, null, versionId, new PersonId(Guid.NewGuid()), default));

        results.Should().BeEquivalentTo(new[] { SelectionChange.Changed, SelectionChange.Unchanged });
        var state = await first.Set<CustomerAgreementState>().SingleAsync();
        var transition = await first.Set<CustomerAgreementTransition>().SingleAsync();
        var acceptance = await first.Set<AgreementAcceptance>().SingleAsync();
        state.ActiveVersionId.Should().Be(versionId);
        transition.FromVersionId.Should().BeNull();
        transition.ToVersionId.Should().Be(versionId);
        transition.Sequence.Should().Be(1);
        acceptance.AcceptedByPersonId.Should().Be(transition.PersonId);
    }

    [Fact]
    public async Task Explicit_and_implicit_acceptance_race_preserves_the_first_acceptance()
    {
        var versionId = await PublishSelectionVersion();
        var gate = new SelectionCommandGate("INSERT INTO agreements.agreement_acceptances");
        await using var selection = SelectionContext(gate);
        await using var acceptance = SelectionContext(gate);
        var relationshipId = (await selection.Versions.SingleAsync()).TradingRelationshipId;
        var explicitOwner = new PersonId(Guid.NewGuid());
        var implicitOwner = new PersonId(Guid.NewGuid());
        var recorded = new AgreementAcceptanceStore(acceptance, TimeProvider.System)
            .AcceptAsync(relationshipId, versionId, explicitOwner, default);
        var changed = new CustomerAgreementSelectionStore(selection, TimeProvider.System)
            .ChangeAsync(relationshipId, null, versionId, implicitOwner, default);

        await Task.WhenAll(recorded, changed);

        (await changed).Should().Be(SelectionChange.Changed);
        var firstAcceptance = await selection.Set<AgreementAcceptance>().SingleAsync();
        firstAcceptance.Should().Be(await recorded);
        new[] { explicitOwner, implicitOwner }.Should().Contain(firstAcceptance.AcceptedByPersonId);
        (await selection.Set<CustomerAgreementState>().SingleAsync()).ActiveVersionId.Should().Be(versionId);
        (await selection.Set<CustomerAgreementTransition>().SingleAsync()).PersonId.Should().Be(implicitOwner);
    }

    [Fact]
    public async Task Failure_after_writing_history_rolls_back_selection_acceptance_and_history()
    {
        var versionId = await PublishSelectionVersion();
        await using var db = SelectionContext(new FailAfterTransitionInsert());
        var relationshipId = (await db.Versions.SingleAsync()).TradingRelationshipId;
        var store = new CustomerAgreementSelectionStore(db, TimeProvider.System);
        var owner = new PersonId(Guid.NewGuid());

        var change = () => store.ChangeAsync(relationshipId, null, versionId, owner, default);
        await change.Should().ThrowAsync<InvalidOperationException>().WithMessage("Injected transition failure");

        (await db.Set<CustomerAgreementState>().AnyAsync()).Should().BeFalse();
        (await db.Set<AgreementAcceptance>().AnyAsync()).Should().BeFalse();
        (await db.Set<CustomerAgreementTransition>().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Deactivation_preserves_acceptance_and_only_records_real_changes()
    {
        var versionId = await PublishSelectionVersion();
        await using var db = SelectionContext();
        var relationshipId = (await db.Versions.SingleAsync()).TradingRelationshipId;
        var store = new CustomerAgreementSelectionStore(db, TimeProvider.System);
        var owner = new PersonId(Guid.NewGuid());

        (await store.ChangeAsync(relationshipId, null, null, owner, default)).Should().Be(SelectionChange.Unchanged);
        (await store.ChangeAsync(relationshipId, null, versionId, owner, default)).Should().Be(SelectionChange.Changed);
        (await store.ChangeAsync(relationshipId, null, null, owner, default)).Should().Be(SelectionChange.Conflict);
        (await store.ChangeAsync(relationshipId, new AgreementVersionId(Guid.NewGuid()), versionId, owner, default))
            .Should().Be(SelectionChange.VersionNotFound);
        (await store.ChangeAsync(relationshipId, versionId, null, owner, default)).Should().Be(SelectionChange.Changed);
        (await store.ChangeAsync(relationshipId, versionId, null, owner, default)).Should().Be(SelectionChange.Unchanged);
        (await store.ChangeAsync(relationshipId, versionId, versionId, owner, default)).Should().Be(SelectionChange.Conflict);

        (await db.Set<CustomerAgreementState>().SingleAsync()).ActiveVersionId.Should().BeNull();
        (await db.Set<AgreementAcceptance>().SingleAsync()).VersionId.Should().Be(versionId);
        var history = await db.Set<CustomerAgreementTransition>().OrderBy(x => x.Sequence).ToListAsync();
        history.Select(x => x.Sequence).Should().Equal(1, 2);
        history[1].FromVersionId.Should().Be(versionId);
        history[1].ToVersionId.Should().BeNull();
    }

    private async Task<AgreementVersionId> PublishSelectionVersion()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var published = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        return new AgreementVersionId((await published.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid());
    }

    private AgreementsDbContext SelectionContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AgreementsDbContext>(fixture.CreateAgreementOptions())
            .AddInterceptors(interceptors).Options);

    private sealed class SelectionCommandGate(string sql) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(sql, StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _arrivals) == 2)
                    _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    private sealed class FailAfterTransitionInsert : DbCommandInterceptor
    {
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO agreements.customer_agreement_transitions", StringComparison.Ordinal))
                throw new InvalidOperationException("Injected transition failure");
            return ValueTask.FromResult(result);
        }
    }
}
