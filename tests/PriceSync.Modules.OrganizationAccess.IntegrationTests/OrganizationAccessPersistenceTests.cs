using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.IntegrationTests;

[Collection(OrganizationAccessPostgreSqlCollection.Name)]
public sealed class OrganizationAccessPersistenceTests(OrganizationAccessPostgreSqlFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Clean_postgresql_database_migrates_from_zero_with_module_local_history()
    {
        var databaseName = $"pricesync_migration_{Guid.NewGuid():N}";
        await ExecuteAsync($"create database {databaseName}");

        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
        };

        try
        {
            var options = new DbContextOptionsBuilder<OrganizationAccessDbContext>()
                .UseNpgsql(
                    builder.ConnectionString,
                    npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                        OrganizationAccessDbContext.MigrationsHistoryTableName,
                        OrganizationAccessDbContext.SchemaName))
                .Options;

            await using var context = new OrganizationAccessDbContext(options);
            await context.Database.MigrateAsync();

            var tables = await QueryStringsAsync(
                builder.ConnectionString,
                """
                select table_name
                from information_schema.tables
                where table_schema = 'organization_access'
                order by table_name;
                """);

            tables.Should().BeEquivalentTo(
                [
                    "__EFMigrationsHistory",
                    "idempotency_records",
                    "organization_membership_roles",
                    "organization_memberships",
                    "organizations",
                ]);
        }
        finally
        {
            await ExecuteAsync($"""drop database "{databaseName}" with (force)""");
        }
    }

    [Fact]
    public async Task Organizations_round_trip_with_strong_ids_identity_status_and_timestamps()
    {
        var organization = NewOrganization(displayName: "  Acme AB  ", organizationNumber: "556016-0680");

        await using (var context = NewContext())
        {
            context.Organizations.Add(organization);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext())
        {
            var loaded = await context.Organizations.SingleAsync(entity => entity.Id == organization.Id);

            loaded.Id.Should().Be(organization.Id);
            loaded.DisplayName.Should().Be("Acme AB");
            loaded.LegalIdentity.RegistrationCountry.Should().Be("SE");
            loaded.LegalIdentity.NationalOrganizationNumber.Should().Be("5560160680");
            loaded.Status.Should().Be(OrganizationStatus.Active);
            loaded.CreatedAt.Should().Be(organization.CreatedAt);
            loaded.ArchivedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task Organization_memberships_round_trip_with_person_status_timestamps_and_explicit_roles()
    {
        var organization = NewOrganization();
        var membership = NewMembership(organization.Id, roles: [OrganizationRole.Owner]);

        await using (var context = NewContext())
        {
            context.Organizations.Add(organization);
            context.OrganizationMemberships.Add(membership);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext())
        {
            var loaded = await context.OrganizationMemberships.SingleAsync(entity => entity.Id == membership.Id);

            loaded.OrganizationId.Should().Be(organization.Id);
            loaded.PersonId.Should().Be(membership.PersonId);
            loaded.Status.Should().Be(OrganizationMembershipStatus.Active);
            loaded.StartedAt.Should().Be(membership.StartedAt);
            loaded.EndedAt.Should().BeNull();
            loaded.Roles.Should().BeEquivalentTo([OrganizationRole.Owner]);
            loaded.HasAuthority(OrganizationRole.Member).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Legal_organization_identity_uniqueness_is_enforced()
    {
        await using var context = NewContext();
        context.Organizations.Add(NewOrganization(organizationNumber: "5560160680"));
        context.Organizations.Add(NewOrganization(organizationNumber: "556016-0680"));

        var save = () => context.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Active_membership_uniqueness_is_enforced_but_multiple_ended_memberships_are_allowed()
    {
        var organization = NewOrganization();
        var personId = NewPersonId();

        await using (var context = NewContext())
        {
            context.Organizations.Add(organization);
            context.OrganizationMemberships.Add(NewEndedMembership(organization.Id, personId));
            context.OrganizationMemberships.Add(NewEndedMembership(organization.Id, personId));
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext())
        {
            context.OrganizationMemberships.Add(NewMembership(organization.Id, personId));
            context.OrganizationMemberships.Add(NewMembership(organization.Id, personId));

            var save = () => context.SaveChangesAsync();

            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Theory]
    [InlineData("organizations", "status", "suspended")]
    [InlineData("organization_memberships", "status", "pending")]
    public async Task Invalid_status_values_are_rejected_by_database_constraints(string tableName, string columnName, string invalidValue)
    {
        var organizationId = await InsertOrganizationAsync();

        var sql = tableName == "organizations"
            ? $"""
               update organization_access.{tableName}
               set {columnName} = '{invalidValue}'
               where id = '{organizationId}';
               """
            : $"""
               insert into organization_access.organization_memberships (id, organization_id, person_id, status, started_at, ended_at)
               values ('{Guid.NewGuid()}', '{organizationId}', '{Guid.NewGuid()}', '{invalidValue}', now(), null);
               """;

        var execute = () => ExecuteAsync(sql);

        await execute.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task Invalid_role_values_and_duplicate_role_rows_are_rejected()
    {
        var organization = NewOrganization();
        var membership = NewMembership(organization.Id, roles: [OrganizationRole.Member]);

        await using (var context = NewContext())
        {
            context.Organizations.Add(organization);
            context.OrganizationMemberships.Add(membership);
            await context.SaveChangesAsync();
        }

        var invalidRole = () => ExecuteAsync(
            $"""
            insert into organization_access.organization_membership_roles (organization_membership_id, role)
            values ('{membership.Id.Value}', 'admin');
            """);

        var duplicateRole = () => ExecuteAsync(
            $"""
            insert into organization_access.organization_membership_roles (organization_membership_id, role)
            values ('{membership.Id.Value}', 'member');
            """);

        await invalidRole.Should().ThrowAsync<PostgresException>();
        await duplicateRole.Should().ThrowAsync<PostgresException>();
    }

    [Theory]
    [InlineData("organizations", "active", "now()")]
    [InlineData("organizations", "archived", "null")]
    [InlineData("organization_memberships", "active", "now()")]
    [InlineData("organization_memberships", "ended", "null")]
    public async Task Lifecycle_status_and_timestamp_consistency_is_enforced(string tableName, string status, string terminalTimestamp)
    {
        var organizationId = await InsertOrganizationAsync();

        var sql = tableName == "organizations"
            ? $"""
               update organization_access.organizations
               set status = '{status}', archived_at = {terminalTimestamp}
               where id = '{organizationId}';
               """
            : $"""
               insert into organization_access.organization_memberships (id, organization_id, person_id, status, started_at, ended_at)
               values ('{Guid.NewGuid()}', '{organizationId}', '{Guid.NewGuid()}', '{status}', now(), {terminalTimestamp});
               """;

        var execute = () => ExecuteAsync(sql);

        await execute.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task Idempotency_record_uniqueness_is_scoped_to_person_operation_and_key()
    {
        var personId = NewPersonId();

        await using (var context = NewContext())
        {
            context.IdempotencyRecords.Add(NewIdempotencyRecord(personId, "create-organization", "same-key"));
            context.IdempotencyRecords.Add(NewIdempotencyRecord(NewPersonId(), "create-organization", "same-key"));
            context.IdempotencyRecords.Add(NewIdempotencyRecord(personId, "archive-organization", "same-key"));
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext())
        {
            context.IdempotencyRecords.Add(NewIdempotencyRecord(personId, "create-organization", "same-key"));

            var save = () => context.SaveChangesAsync();

            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task Transaction_rollback_leaves_no_partial_organization_access_data()
    {
        await using (var context = NewContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var organization = NewOrganization();
            context.Organizations.Add(organization);
            context.OrganizationMemberships.Add(NewMembership(organization.Id));
            context.IdempotencyRecords.Add(NewIdempotencyRecord(NewPersonId(), "create-organization", "rollback-key"));
            await context.SaveChangesAsync();

            await transaction.RollbackAsync();
        }

        (await CountRowsAsync("organizations")).Should().Be(0);
        (await CountRowsAsync("organization_memberships")).Should().Be(0);
        (await CountRowsAsync("organization_membership_roles")).Should().Be(0);
        (await CountRowsAsync("idempotency_records")).Should().Be(0);
    }

    [Fact]
    public void Persistence_infrastructure_remains_internal_to_organization_access()
    {
        var exportedPersistenceTypes = typeof(OrganizationAccessDbContext).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Where(typeName => typeName is not null && typeName.Contains(".Persistence.", StringComparison.Ordinal));

        exportedPersistenceTypes.Should().BeEmpty();
    }

    private OrganizationAccessDbContext NewContext() => new(fixture.CreateOptions());

    private async Task<Guid> InsertOrganizationAsync()
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            $"""
            insert into organization_access.organizations
                (id, display_name, registration_country, national_organization_number, status, created_at, archived_at)
            values
                ('{id}', 'Acme AB', 'SE', '{Random.Shared.Next(100000000, 999999999)}0', 'active', now(), null);
            """);

        return id;
    }

    private async Task<int> CountRowsAsync(string tableName)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"select count(*) from organization_access.{tableName};", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyCollection<string>> QueryStringsAsync(string connectionString, string sql)
    {
        var values = new List<string>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static Organization NewOrganization(string displayName = "Acme AB", string organizationNumber = "5560160680")
    {
        return Organization.Create(
            NewOrganizationId(),
            displayName,
            LegalOrganizationIdentity.Create("SE", organizationNumber),
            NewFrozenTimeProvider());
    }

    private static OrganizationMembership NewMembership(
        OrganizationId organizationId,
        PersonId? personId = null,
        OrganizationRole[]? roles = null)
    {
        return OrganizationMembership.Create(
            NewMembershipId(),
            organizationId,
            personId ?? NewPersonId(),
            roles ?? [OrganizationRole.Member],
            NewFrozenTimeProvider());
    }

    private static OrganizationMembership NewEndedMembership(OrganizationId organizationId, PersonId personId)
    {
        var membership = NewMembership(organizationId, personId);
        membership.End(new FrozenTimeProvider(new DateTimeOffset(2026, 9, 10, 14, 45, 0, TimeSpan.Zero)));
        return membership;
    }

    private static IdempotencyRecord NewIdempotencyRecord(PersonId personId, string operation, string key)
    {
        return new IdempotencyRecord(
            personId,
            operation,
            key,
            [1, 2, 3],
            201,
            """{"id":"created"}""",
            new DateTimeOffset(2026, 9, 10, 12, 30, 0, TimeSpan.Zero));
    }

    private static OrganizationId NewOrganizationId() => new(Guid.NewGuid());

    private static OrganizationMembershipId NewMembershipId() => new(Guid.NewGuid());

    private static PersonId NewPersonId() => new(Guid.NewGuid());

    private static FrozenTimeProvider NewFrozenTimeProvider() => new(new DateTimeOffset(2026, 9, 10, 12, 30, 0, TimeSpan.Zero));

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
