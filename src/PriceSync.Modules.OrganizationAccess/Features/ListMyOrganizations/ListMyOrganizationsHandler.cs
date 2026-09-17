using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Persistence;

namespace PriceSync.Modules.OrganizationAccess.Features.ListMyOrganizations;

internal sealed class ListMyOrganizationsHandler(
    OrganizationAccessDbContext dbContext,
    ICurrentPerson currentPerson)
{
    public async Task<IReadOnlyCollection<ListMyOrganizationsResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var personId = currentPerson.PersonId;

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select
                o.id,
                o.display_name,
                o.registration_country,
                o.national_organization_number,
                o.status,
                r.role
            from {OrganizationAccessDbContext.SchemaName}.organizations o
            join {OrganizationAccessDbContext.SchemaName}.organization_memberships m
                on m.organization_id = o.id
            join {OrganizationAccessDbContext.SchemaName}.organization_membership_roles r
                on r.organization_membership_id = m.id
            where m.person_id = @person_id
                and m.status = 'active'
            order by
                case o.status when 'active' then 0 else 1 end,
                o.display_name,
                o.id,
                r.role;
            """;
        command.Parameters.Add(new NpgsqlParameter<Guid>("person_id", personId.Value));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var organizations = new List<ListMyOrganizationsResponse>();
        ListOrganizationBuilder? current = null;

        while (await reader.ReadAsync(cancellationToken))
        {
            var organizationId = reader.GetGuid(0);
            if (current is null || current.OrganizationId != organizationId)
            {
                if (current is not null)
                {
                    organizations.Add(current.ToResponse());
                }

                current = new ListOrganizationBuilder(
                    organizationId,
                    reader.GetString(1),
                    reader.GetString(2).Trim(),
                    reader.GetString(3),
                    reader.GetString(4));
            }

            current.Roles.Add(reader.GetString(5));
        }

        if (current is not null)
        {
            organizations.Add(current.ToResponse());
        }

        return organizations;
    }

    private sealed class ListOrganizationBuilder(
        Guid organizationId,
        string displayName,
        string registrationCountry,
        string organizationNumber,
        string status)
    {
        public Guid OrganizationId { get; } = organizationId;

        public List<string> Roles { get; } = [];

        public ListMyOrganizationsResponse ToResponse()
        {
            return new ListMyOrganizationsResponse(
                OrganizationId,
                displayName,
                registrationCountry,
                organizationNumber,
                status,
                Roles);
        }
    }
}
