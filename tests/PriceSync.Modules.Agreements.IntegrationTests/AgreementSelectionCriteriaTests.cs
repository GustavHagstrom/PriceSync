using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Existing_versions_from_another_relationship_are_rejected_on_either_side_before_comparison()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var current = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var supplierId = new OrganizationId(Guid.Parse(supplier.DefaultRequestHeaders.GetValues("organization_id").Single()));
        var otherCustomer = await SeedOrganization(new PersonId(Guid.CreateVersion7()), "5560004615");
        var otherRelationship = await SeedRelationship(supplierId, otherCustomer);
        var foreign = await PublishSiblingSelectionTerms(supplier, otherRelationship.Id.Value, scope.List);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        var url = SelectionUrl(current.RelationshipId);
        await ChangeSelection(customer, url, null, current.Id);

        using var foreignFrom = await PutSelection(customer, url, foreign.Id, current.Id);
        using var foreignTo = await PutSelection(customer, url, current.Id, foreign.Id);
        foreach (var response in new[] { foreignFrom, foreignTo })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
                .Should().Be("agreement_version_not_found");
        }
        (await ReadActiveSelection(customer, url)).Should().Be(current.Id);
        await using var db = SelectionContext();
        (await db.Set<CustomerAgreementTransition>().CountAsync()).Should().Be(1);
        (await db.Set<AgreementAcceptance>().CountAsync()).Should().Be(1);

        var otherOwner = await AddAcceptanceActor(otherCustomer, OrganizationRole.Owner);
        await using var otherCustomerApp = new ApiApplication(fixture.ConnectionString, otherOwner);
        using var otherClient = AcceptanceClient(otherCustomerApp, otherCustomer);
        await ChangeSelection(otherClient, SelectionUrl(foreign.RelationshipId), null, foreign.Id);
        (await ReadActiveSelection(customer, url)).Should().Be(current.Id);
        var history = await db.Set<CustomerAgreementTransition>().ToListAsync();
        history.Select(x => x.TradingRelationshipId.Value).Should()
            .BeEquivalentTo(new[] { current.RelationshipId, foreign.RelationshipId });
        history.Should().OnlyContain(x => x.Sequence == 1);
    }

    [Fact]
    public async Task Actual_decisions_use_the_system_clock_and_retries_preserve_the_first_decision_and_acceptance()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var version = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var url = SelectionUrl(version.RelationshipId);
        var acceptanceUrl = $"{version.Location}/acceptance";
        var firstOwner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        var secondOwner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        var clock = new ListTestClock(new DateTimeOffset(2026, 11, 27, 8, 0, 0, TimeSpan.Zero));
        void SetClock(IServiceCollection services)
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }
        await using var firstApp = new ApiApplication(fixture.ConnectionString, firstOwner, SetClock);
        await using var secondApp = new ApiApplication(fixture.ConnectionString, secondOwner, SetClock);
        using var first = AcceptanceClient(firstApp, scope.Customer);
        using var second = AcceptanceClient(secondApp, scope.Customer);
        using var accepted = await first.PostAsync(acceptanceUrl, null);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        var acceptance = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        acceptance.GetProperty("acceptedAt").GetDateTimeOffset().Should().Be(clock.Now);
        acceptance.GetProperty("acceptedByPersonId").GetGuid().Should().Be(firstOwner.Value);

        clock.Now = clock.Now.AddHours(1);
        using var activation = await second.PutAsJsonAsync(url, new
        {
            fromVersionId = (Guid?)null, toVersionId = version.Id, decidedAt = clock.Now.AddYears(-1),
        });
        activation.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = SelectionContext();
        var decision = await db.Set<CustomerAgreementTransition>().AsNoTracking().SingleAsync();
        decision.DecidedAt.Should().Be(clock.Now);
        decision.PersonId.Should().Be(secondOwner);

        clock.Now = clock.Now.AddHours(1);
        await ChangeSelection(first, url, version.Id, version.Id);
        using var conflict = await PutSelection(first, url, null, null);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await db.Set<CustomerAgreementTransition>().AsNoTracking().SingleAsync()).Should().Be(decision);

        using var deactivation = await second.PutAsJsonAsync(url, new
        {
            fromVersionId = version.Id, toVersionId = (Guid?)null, decidedAt = clock.Now.AddYears(1),
        });
        deactivation.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadActiveSelection(second, url)).Should().BeNull();

        var deactivatedAt = clock.Now;
        clock.Now = clock.Now.AddHours(1);
        await ChangeSelection(second, url, null, version.Id);
        (await ReadActiveSelection(second, url)).Should().Be(version.Id);
        var history = await db.Set<CustomerAgreementTransition>().OrderBy(x => x.Sequence).ToListAsync();
        history.Select(x => x.Sequence).Should().Equal(1, 2, 3);
        history.Select(x => x.DecidedAt).Should().Equal(decision.DecidedAt, deactivatedAt, clock.Now);
        JsonElement.DeepEquals(acceptance, await supplier.GetFromJsonAsync<JsonElement>(acceptanceUrl)).Should().BeTrue();
    }

    [Fact]
    public async Task Archived_customer_organization_cannot_read_or_repeat_an_active_selection()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var version = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var url = SelectionUrl(version.RelationshipId);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        await ChangeSelection(customer, url, null, version.Id);

        await using (var db = new OrganizationAccessDbContext(fixture.CreateOptions()))
        {
            (await db.Organizations.SingleAsync(x => x.Id == scope.Customer)).Archive(TimeProvider.System);
            await db.SaveChangesAsync();
        }

        using var retry = await PutSelection(customer, url, null, version.Id);
        using var read = await customer.GetAsync(url);
        retry.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadActiveSelection(supplier, url)).Should().Be(version.Id);
    }

    [Fact]
    public async Task PostgreSQL_rejects_active_versions_without_a_matching_acceptance_and_foreign_history_references()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var version = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        await using var db = SelectionContext();
        var wrongRelationship = Guid.CreateVersion7();

        Task InsertActive(Guid relationship) => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO agreements.customer_agreement_states ("TradingRelationshipId", "ActiveVersionId", "Sequence")
            VALUES ({relationship}, {version.Id}, 1)
            """);
        await ExpectForeignKeyViolation(() => InsertActive(version.RelationshipId));
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        await ChangeSelection(customer, SelectionUrl(version.RelationshipId), null, version.Id);
        await ExpectForeignKeyViolation(() => InsertActive(wrongRelationship));
        await ExpectForeignKeyViolation(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO agreements.agreement_acceptances ("TradingRelationshipId", "VersionId", "AcceptedByPersonId", "AcceptedAt")
            VALUES ({wrongRelationship}, {version.Id}, {owner.Value}, CURRENT_TIMESTAMP)
            """));

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO agreements.customer_agreement_states ("TradingRelationshipId", "Sequence")
            VALUES ({wrongRelationship}, 0)
            """);
        await ExpectForeignKeyViolation(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO agreements.customer_agreement_transitions
                ("TradingRelationshipId", "Sequence", "FromVersionId", "ToVersionId", "PersonId", "DecidedAt")
            VALUES ({wrongRelationship}, 1, {version.Id}, NULL, {owner.Value}, CURRENT_TIMESTAMP)
            """));
        await ExpectForeignKeyViolation(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO agreements.customer_agreement_transitions
                ("TradingRelationshipId", "Sequence", "FromVersionId", "ToVersionId", "PersonId", "DecidedAt")
            VALUES ({wrongRelationship}, 1, NULL, {version.Id}, {owner.Value}, CURRENT_TIMESTAMP)
            """));
    }

    private static async Task ExpectForeignKeyViolation(Func<Task> write)
    {
        var thrown = await write.Should().ThrowAsync<PostgresException>();
        thrown.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Selection_holds_the_relationship_row_lock_in_a_read_committed_transaction()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var version = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var url = SelectionUrl(version.RelationshipId);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        await ChangeSelection(customer, url, null, version.Id);

        var pause = new PauseSelectionCommand("UPDATE agreements.customer_agreement_states");
        await using var delayedApp = SelectionApi(owner, pause);
        using var delayed = AcceptanceClient(delayedApp, scope.Customer);
        var pending = PutSelection(delayed, url, version.Id, null);
        try
        {
            await pause.WaitForArrivalAsync();
            pause.IsolationLevel.Should().Be(IsolationLevel.ReadCommitted);
            await using var db = SelectionContext();
            Func<Task> takeLock = () => db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT 1 FROM agreements.customer_agreement_states
                WHERE "TradingRelationshipId" = {version.RelationshipId}
                FOR UPDATE NOWAIT
                """);
            (await takeLock.Should().ThrowAsync<PostgresException>()).Which.SqlState
                .Should().Be(PostgresErrorCodes.LockNotAvailable);
        }
        finally
        {
            pause.Release();
        }

        using var completed = await pending;
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Selecting_a_version_without_coverage_for_a_list_preserves_list_data_and_returns_exactly_that_version()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var original = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        using var createdList = await supplier.PostAsJsonAsync("/api/v1/price-lists", new
        {
            name = "Separate list", type = "general", currency = "SEK", market = "SE",
        });
        createdList.StatusCode.Should().Be(HttpStatusCode.Created);
        var otherList = (await createdList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var replacement = await PublishSiblingSelectionTerms(supplier, original.RelationshipId, otherList);
        var listUrl = $"/api/v1/price-lists/{scope.List}";
        var savedList = await supplier.GetFromJsonAsync<JsonElement>(listUrl);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner,
            services => services.AddScoped<IPriceListScopeVerifier, UnexpectedSelectionListVerification>());
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        var url = SelectionUrl(original.RelationshipId);
        await ChangeSelection(customer, url, null, original.Id);
        await ChangeSelection(customer, url, original.Id, replacement.Id);

        JsonElement.DeepEquals(savedList, await supplier.GetFromJsonAsync<JsonElement>(listUrl)).Should().BeTrue();
        await using var services = customerApp.Services.CreateAsyncScope();
        var reader = services.ServiceProvider.GetRequiredService<ICustomerAgreementSelectionReader>();
        var versions = services.ServiceProvider.GetRequiredService<IAgreementVersionReader>();
        var active = await reader.GetActiveVersionIdAsync(TradingRelationshipId.FromGuid(original.RelationshipId));
        active.Should().Be(new AgreementVersionId(replacement.Id));
        var terms = await versions.GetAsync(active!.Value);
        terms!.Coverage.Select(item => item.PriceListId.Value).Should().Equal(otherList);
    }

    private sealed class UnexpectedSelectionListVerification : IPriceListScopeVerifier
    {
        public Task<PriceListVerificationResult> VerifyAsync(OrganizationId supplierOrganizationId,
            OrganizationId customerOrganizationId, IReadOnlyCollection<PriceListReference> references,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Selection must not reverify published list coverage.");
    }
}
