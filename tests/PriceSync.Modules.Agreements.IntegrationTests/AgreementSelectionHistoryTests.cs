using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Historical_reads_preserve_persisted_state_and_only_return_actual_customer_choices()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var selected = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        var url = SelectionUrl(selected.RelationshipId);
        await ChangeSelection(customer, url, null, selected.Id);
        var unselected = await PublishSelectionTerms(supplier, scope.Agreement, scope.List, selected.Id);
        using var withdrawal = await supplier.PostAsJsonAsync($"{selected.Location}/withdraw", new { reason = "Superseded" });
        withdrawal.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = async (AgreementsDbContext db) => new
        {
            Selections = await db.Set<CustomerAgreementState>().AsNoTracking().ToListAsync(),
            Transitions = await db.Set<CustomerAgreementTransition>().AsNoTracking().ToListAsync(),
            Acceptances = await db.Set<AgreementAcceptance>().AsNoTracking().ToListAsync(),
            Agreements = await db.PriceAgreements.AsNoTracking().ToListAsync(),
            Versions = await db.Versions.AsNoTracking().ToListAsync(),
            Coverage = await db.Coverage.AsNoTracking().ToListAsync(),
            GroupDiscounts = await db.GroupDiscounts.AsNoTracking().ToListAsync(),
            Withdrawals = await db.Withdrawals.AsNoTracking().ToListAsync(),
        };
        await using var before = SelectionContext();
        var expected = await snapshot(before);

        await using var services = app.Services.CreateAsyncScope();
        var reader = services.ServiceProvider.GetRequiredService<ICustomerAgreementSelectionReader>();
        var versions = services.ServiceProvider.GetRequiredService<IAgreementVersionReader>();
        var relationship = TradingRelationshipId.FromGuid(selected.RelationshipId);
        var selectedId = new AgreementVersionId(selected.Id);
        var unselectedId = new AgreementVersionId(unselected.Id);
        var unselectedTerms = await versions.GetAsync(unselectedId);
        unselectedTerms.Should().NotBeNull();
        var history = (await reader.GetHistoryAsync(relationship)).Items.Should().ContainSingle().Which;
        history.FromVersionId.Should().BeNull();
        history.ToVersionId.Should().Be(selectedId);
        (await reader.GetActiveVersionIdAsync(relationship, null, long.MaxValue)).Should().Be(selectedId);

        var page = await supplier.GetFromJsonAsync<JsonElement>($"{url}/history");
        page.GetProperty("items").EnumerateArray().Single()
            .GetProperty("toVersionId").GetGuid().Should().Be(selected.Id);
        var asOf = unselectedTerms!.PublishedAt > history.DecidedAt ? unselectedTerms.PublishedAt : history.DecidedAt;
        var cutoff = Uri.EscapeDataString(asOf.ToString("O"));
        (await supplier.GetFromJsonAsync<JsonElement>($"{url}?asOfUtc={cutoff}"))
            .GetProperty("activeVersionId").GetGuid().Should().Be(selected.Id);

        await using var after = SelectionContext();
        (await snapshot(after)).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Historical_selection_reconstructs_switches_and_deactivation_from_inclusive_boundaries()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var first = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var second = await PublishSiblingSelectionTerms(supplier, first.RelationshipId, scope.List);
        var relationship = TradingRelationshipId.FromGuid(first.RelationshipId);
        var firstId = new AgreementVersionId(first.Id);
        var secondId = new AgreementVersionId(second.Id);
        var person = new PersonId(Guid.NewGuid());
        var switchedAt = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        var clock = new ListTestClock(switchedAt);
        await using var writer = SelectionContext();
        var store = new CustomerAgreementSelectionStore(writer, clock);
        await using var db = SelectionContext();
        var reader = new CustomerAgreementSelectionReader(db);

        (await reader.GetActiveVersionIdAsync(relationship, asOfUtc: switchedAt, throughSequence: null))
            .Should().BeNull();
        (await store.ChangeAsync(relationship, null, firstId, person, default)).Should().Be(SelectionChange.Changed);
        clock.Now = switchedAt.AddMinutes(-1);
        (await store.ChangeAsync(relationship, firstId, secondId, person, default)).Should().Be(SelectionChange.Changed);
        clock.Now = switchedAt.AddMinutes(1);
        (await store.ChangeAsync(relationship, secondId, null, person, default)).Should().Be(SelectionChange.Changed);

        (await reader.GetActiveVersionIdAsync(relationship, asOfUtc: switchedAt.AddSeconds(-1), throughSequence: null))
            .Should().BeNull();
        (await reader.GetActiveVersionIdAsync(relationship, asOfUtc: switchedAt, throughSequence: null))
            .Should().Be(secondId);
        (await reader.GetActiveVersionIdAsync(relationship, asOfUtc: clock.Now, throughSequence: null))
            .Should().BeNull();
        (await reader.GetActiveVersionIdAsync(relationship, asOfUtc: null, throughSequence: 0)).Should().BeNull();
        (await reader.GetActiveVersionIdAsync(relationship, asOfUtc: null, throughSequence: 1)).Should().Be(firstId);
        (await reader.GetActiveVersionIdAsync(relationship, asOfUtc: null, throughSequence: 2)).Should().Be(secondId);
        (await reader.GetActiveVersionIdAsync(relationship, asOfUtc: null, throughSequence: long.MaxValue))
            .Should().BeNull();
        (await reader.GetActiveVersionIdAsync(relationship, asOfUtc: null, throughSequence: null))
            .Should().BeNull();
        var bothBoundaries = () => reader.GetActiveVersionIdAsync(relationship, switchedAt, 1);
        await bothBoundaries.Should().ThrowAsync<ArgumentException>();
        var negativeSequence = () => reader.GetActiveVersionIdAsync(relationship, null, -1);
        await negativeSequence.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Selection_history_pages_forward_by_sequence_even_when_timestamps_tie()
    {
        var versionId = await PublishSelectionVersion();
        await using var writer = SelectionContext();
        var relationship = (await writer.Versions.SingleAsync()).TradingRelationshipId;
        var person = new PersonId(Guid.NewGuid());
        var timestamp = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        var store = new CustomerAgreementSelectionStore(writer, new ListTestClock(timestamp));
        AgreementVersionId? active = null;
        for (var sequence = 1; sequence <= 101; sequence++)
        {
            AgreementVersionId? next = active is null ? versionId : null;
            (await store.ChangeAsync(relationship, active, next, person, default)).Should().Be(SelectionChange.Changed);
            active = next;
        }

        await using var db = SelectionContext();
        var reader = new CustomerAgreementSelectionReader(db);
        var first = await reader.GetHistoryAsync(relationship);
        first.Items.Select(x => x.Sequence).Should().Equal(Enumerable.Range(1, 50).Select(x => (long)x));
        first.NextAfterSequence.Should().Be(50);
        first.Items[0].Should().Be(new CustomerAgreementHistoryItem(1, null, versionId, person, timestamp));
        first.Items[1].Should().Be(new CustomerAgreementHistoryItem(2, versionId, null, person, timestamp));

        var second = await reader.GetHistoryAsync(relationship, afterSequence: first.NextAfterSequence, pageSize: 100);
        second.Items.Select(x => x.Sequence).Should().Equal(Enumerable.Range(51, 51).Select(x => (long)x));
        second.NextAfterSequence.Should().BeNull();
        (await reader.GetHistoryAsync(relationship, pageSize: 100)).NextAfterSequence.Should().Be(100);

        var exhausted = await reader.GetHistoryAsync(relationship, afterSequence: 101);
        exhausted.Items.Should().BeEmpty();
        exhausted.NextAfterSequence.Should().BeNull();
        var unrelated = await reader.GetHistoryAsync(TradingRelationshipId.FromGuid(Guid.NewGuid()));
        unrelated.Items.Should().BeEmpty();
        unrelated.NextAfterSequence.Should().BeNull();
    }

    [Theory]
    [InlineData(-1L, 50)]
    [InlineData(0L, 0)]
    [InlineData(0L, 101)]
    public async Task Selection_history_rejects_invalid_pagination(long afterSequence, int pageSize)
    {
        await using var db = SelectionContext();
        var reader = new CustomerAgreementSelectionReader(db);
        var read = () => reader.GetHistoryAsync(TradingRelationshipId.FromGuid(Guid.NewGuid()), afterSequence, pageSize);
        await read.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
