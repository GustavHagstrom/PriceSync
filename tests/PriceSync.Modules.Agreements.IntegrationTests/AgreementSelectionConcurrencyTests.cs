using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Theory]
    [InlineData("first activation")]
    [InlineData("identical switch")]
    [InlineData("competing switches")]
    [InlineData("deactivation")]
    public async Task Concurrent_selection_requests_append_only_one_transition(string scenario)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var a = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var b = await PublishSiblingSelectionTerms(supplier, a.RelationshipId, scope.List);
        var c = await PublishSiblingSelectionTerms(supplier, a.RelationshipId, scope.List);
        var url = SelectionUrl(a.RelationshipId);
        var firstOwner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        var secondOwner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var initialApp = new ApiApplication(fixture.ConnectionString, firstOwner);
        using var initial = AcceptanceClient(initialApp, scope.Customer);
        (Guid?, Guid?, Guid?) transitions = scenario switch
        {
            "first activation" => (null, b.Id, b.Id),
            "identical switch" => (a.Id, b.Id, b.Id),
            "competing switches" => (a.Id, b.Id, c.Id),
            "deactivation" => (a.Id, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown selection scenario."),
        };
        var (from, firstTo, secondTo) = transitions;
        var competing = firstTo != secondTo;
        if (from is not null)
            await ChangeSelection(initial, url, null, from);

        var gate = new SelectionCommandGate("INSERT INTO agreements.customer_agreement_states");
        await using var firstApp = SelectionApi(firstOwner, gate);
        await using var secondApp = SelectionApi(secondOwner, gate);
        using var first = AcceptanceClient(firstApp, scope.Customer);
        using var second = AcceptanceClient(secondApp, scope.Customer);
        var firstChange = PutSelection(first, url, from, firstTo);
        var secondChange = PutSelection(second, url, from, secondTo);
        await Task.WhenAll(firstChange, secondChange);
        using var firstResponse = await firstChange;
        using var secondResponse = await secondChange;

        var expectedStatuses = competing
            ? new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }
            : new[] { HttpStatusCode.OK, HttpStatusCode.OK };
        new[] { firstResponse.StatusCode, secondResponse.StatusCode }.Should().BeEquivalentTo(expectedStatuses);
        var expected = firstResponse.IsSuccessStatusCode ? firstTo : secondTo;
        (await ReadActiveSelection(supplier, url)).Should().Be(expected);
        await using var db = SelectionContext();
        var history = await db.Set<CustomerAgreementTransition>().OrderBy(x => x.Sequence).ToListAsync();
        history.Select(x => x.Sequence).Should().Equal(from is null ? new long[] { 1 } : new long[] { 1, 2 });
        (history[^1].FromVersionId?.Value).Should().Be(from);
        (history[^1].ToVersionId?.Value).Should().Be(expected);
        new[] { firstOwner, secondOwner }.Should().Contain(history[^1].PersonId);
        if (competing)
        {
            var conflict = firstResponse.IsSuccessStatusCode ? secondResponse : firstResponse;
            (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
                .Should().Be("agreement_selection_conflict");
            history[^1].PersonId.Should().Be(firstResponse.IsSuccessStatusCode ? firstOwner : secondOwner);
            var losingVersion = firstResponse.IsSuccessStatusCode ? c : b;
            using var acceptance = await supplier.GetAsync($"{losingVersion.Location}/acceptance");
            acceptance.StatusCode.Should().Be(HttpStatusCode.OK);
            (await acceptance.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accepted").GetBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public async Task A_delayed_switch_can_complete_after_the_selection_returns_to_its_from_version()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var a = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var b = await PublishSiblingSelectionTerms(supplier, a.RelationshipId, scope.List);
        var c = await PublishSiblingSelectionTerms(supplier, a.RelationshipId, scope.List);
        var url = SelectionUrl(a.RelationshipId);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        await ChangeSelection(customer, url, null, a.Id);

        var pause = new PauseSelectionCommand();
        await using var delayedApp = SelectionApi(owner, pause);
        using var delayed = AcceptanceClient(delayedApp, scope.Customer);
        var pending = PutSelection(delayed, url, a.Id, b.Id);
        try
        {
            await pause.WaitForArrivalAsync();
            await ChangeSelection(customer, url, a.Id, c.Id);
            await ChangeSelection(customer, url, c.Id, a.Id);
        }
        finally
        {
            pause.Release();
        }

        using var response = await pending;
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadActiveSelection(customer, url)).Should().Be(b.Id);
        await using var db = SelectionContext();
        var history = await db.Set<CustomerAgreementTransition>().OrderBy(x => x.Sequence).ToListAsync();
        history.Select(x => x.ToVersionId!.Value.Value).Should().Equal(a.Id, c.Id, a.Id, b.Id);
    }

    [Fact]
    public async Task Simultaneous_explicit_acceptance_and_activation_share_one_first_acceptance()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var version = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var url = SelectionUrl(version.RelationshipId);
        var acceptanceUrl = $"{version.Location}/acceptance";
        var explicitOwner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        var activatingOwner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        var gate = new SelectionCommandGate("INSERT INTO agreements.agreement_acceptances");
        await using var explicitApp = SelectionApi(explicitOwner, gate);
        await using var activatingApp = SelectionApi(activatingOwner, gate);
        using var accepting = AcceptanceClient(explicitApp, scope.Customer);
        using var activating = AcceptanceClient(activatingApp, scope.Customer);

        var accept = accepting.PostAsync(acceptanceUrl, null);
        var activate = PutSelection(activating, url, null, version.Id);
        await Task.WhenAll(accept, activate);
        using var accepted = await accept;
        using var activated = await activate;
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        activated.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        new[] { explicitOwner.Value, activatingOwner.Value }.Should().Contain(first.GetProperty("acceptedByPersonId").GetGuid());
        JsonElement.DeepEquals(first, await supplier.GetFromJsonAsync<JsonElement>(acceptanceUrl)).Should().BeTrue();
        (await ReadActiveSelection(supplier, url)).Should().Be(version.Id);
        await using var db = SelectionContext();
        (await db.Set<AgreementAcceptance>().CountAsync()).Should().Be(1);
        (await db.Set<CustomerAgreementTransition>().SingleAsync()).PersonId.Should().Be(activatingOwner);
    }

    [Fact]
    public async Task Failed_switch_leaves_the_previous_selection_and_acceptance_history_intact()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var a = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var b = await PublishSiblingSelectionTerms(supplier, a.RelationshipId, scope.List);
        var url = SelectionUrl(a.RelationshipId);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        await using var failingApp = SelectionApi(owner, new FailAfterTransitionInsert());
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        using var failing = AcceptanceClient(failingApp, scope.Customer);
        await ChangeSelection(customer, url, null, a.Id);
        var firstAcceptance = await supplier.GetFromJsonAsync<JsonElement>($"{a.Location}/acceptance");

        using var failure = await PutSelection(failing, url, a.Id, b.Id);
        failure.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await ReadActiveSelection(supplier, url)).Should().Be(a.Id);
        JsonElement.DeepEquals(firstAcceptance, await supplier.GetFromJsonAsync<JsonElement>($"{a.Location}/acceptance"))
            .Should().BeTrue();
        var rolledBack = await supplier.GetFromJsonAsync<JsonElement>($"{b.Location}/acceptance");
        rolledBack.GetProperty("accepted").GetBoolean().Should().BeFalse();
        await using var db = SelectionContext();
        (await db.Set<CustomerAgreementTransition>().SingleAsync()).Sequence.Should().Be(1);

        await ChangeSelection(customer, url, a.Id, b.Id);
        (await ReadActiveSelection(supplier, url)).Should().Be(b.Id);
        (await db.Set<CustomerAgreementTransition>().OrderBy(x => x.Sequence).Select(x => x.Sequence).ToListAsync())
            .Should().Equal(1, 2);
    }

    [Fact]
    public async Task Publication_and_withdrawal_preserve_selection_and_an_in_flight_switch_can_finish_after_termination()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var a = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var url = SelectionUrl(a.RelationshipId);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        await ChangeSelection(customer, url, null, a.Id);

        var b = await PublishSelectionTerms(supplier, scope.Agreement, scope.List, a.Id);
        (await ReadActiveSelection(supplier, url)).Should().Be(a.Id);
        using var withdrawal = await supplier.PostAsJsonAsync($"{a.Location}/withdraw", new { reason = "Superseded" });
        withdrawal.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadActiveSelection(supplier, url)).Should().Be(a.Id);

        var pause = new PauseSelectionCommand();
        await using var delayedApp = SelectionApi(owner, pause);
        using var delayed = AcceptanceClient(delayedApp, scope.Customer);
        var pending = PutSelection(delayed, url, a.Id, b.Id);
        try
        {
            await pause.WaitForArrivalAsync();
            using var ended = await supplier.PostAsync($"/api/v1/trading-relationships/{a.RelationshipId}/end", null);
            ended.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ReadActiveSelection(supplier, url)).Should().Be(a.Id);
        }
        finally
        {
            pause.Release();
        }

        using var completed = await pending;
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadActiveSelection(supplier, url)).Should().Be(b.Id);
        using var subsequent = await PutSelection(customer, url, b.Id, a.Id);
        subsequent.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await subsequent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("agreement_relationship_ended");
        var relationship = await supplier.GetFromJsonAsync<JsonElement>($"/api/v1/trading-relationships/{a.RelationshipId}");
        relationship.GetProperty("status").GetString().Should().Be("ended");
        await using var db = SelectionContext();
        var history = await db.Set<CustomerAgreementTransition>().OrderBy(x => x.Sequence).ToListAsync();
        history.Select(x => x.ToVersionId!.Value.Value).Should().Equal(a.Id, b.Id);
    }

    private async Task<(Guid Id, Guid RelationshipId, string Location)> PublishSiblingSelectionTerms(
        HttpClient supplier, Guid relationshipId, Guid list)
    {
        using var agreement = await PostAgreement(supplier, relationshipId);
        agreement.StatusCode.Should().Be(HttpStatusCode.Created);
        return await PublishSelectionTerms(supplier, agreement.Headers.Location!.ToString(), list);
    }

    private static async Task<(Guid Id, Guid RelationshipId, string Location)> PublishSelectionTerms(
        HttpClient supplier, string agreement, Guid list, Guid? predecessor = null)
    {
        var terms = Publication(list);
        if (predecessor is { } version)
            terms["expectedLatestVersionId"] = version;
        using var response = await supplier.PostAsJsonAsync($"{agreement}/versions", terms);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var published = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (published.GetProperty("id").GetGuid(), published.GetProperty("tradingRelationshipId").GetGuid(),
            response.Headers.Location!.ToString());
    }

    private ApiApplication SelectionApi(PersonId owner, IInterceptor interceptor) =>
        new(fixture.ConnectionString, owner,
            services => services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(interceptor)));

    private static string SelectionUrl(Guid relationshipId) =>
        $"/api/v1/trading-relationships/{relationshipId}/agreement-selection";

    private static Task<HttpResponseMessage> PutSelection(HttpClient client, string url, Guid? from, Guid? to) =>
        client.PutAsJsonAsync(url, new { fromVersionId = from, toVersionId = to });

    private static async Task ChangeSelection(HttpClient client, string url, Guid? from, Guid? to)
    {
        using var response = await PutSelection(client, url, from, to);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<Guid?> ReadActiveSelection(HttpClient client, string url)
    {
        var response = await client.GetFromJsonAsync<JsonElement>(url);
        var selected = response.GetProperty("activeVersionId");
        return selected.ValueKind == JsonValueKind.Null ? null : selected.GetGuid();
    }

    private sealed class PauseSelectionCommand(
        string sql = "INSERT INTO agreements.customer_agreement_states") : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal System.Data.IsolationLevel? IsolationLevel { get; private set; }

        internal Task WaitForArrivalAsync() => _arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        internal void Release() => _released.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(sql, StringComparison.Ordinal))
            {
                IsolationLevel = command.Transaction?.IsolationLevel;
                _arrived.TrySetResult();
                await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
