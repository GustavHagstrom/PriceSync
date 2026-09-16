using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Persistence;

namespace PriceSync.Modules.OrganizationAccess.Features.GetOrganization;

internal sealed class GetOrganizationHandler(
    OrganizationAccessDbContext dbContext,
    ICurrentPerson currentPerson)
{
    public async Task<GetOrganizationResponse?> HandleAsync(
        OrganizationId organizationId,
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
            where o.id = @organization_id
                and m.person_id = @person_id
                and m.status = 'active'
            order by r.role;
            """;
        command.Parameters.Add(new NpgsqlParameter<Guid>("organization_id", organizationId.Value));
        command.Parameters.Add(new NpgsqlParameter<Guid>("person_id", personId.Value));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        Guid? responseOrganizationId = null;
        string? displayName = null;
        string? registrationCountry = null;
        string? organizationNumber = null;
        string? status = null;
        var roles = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            responseOrganizationId ??= reader.GetGuid(0);
            displayName ??= reader.GetString(1);
            registrationCountry ??= reader.GetString(2).Trim();
            organizationNumber ??= reader.GetString(3);
            status ??= reader.GetString(4);
            roles.Add(reader.GetString(5));
        }

        if (responseOrganizationId is not { } id)
        {
            return null;
        }

        return new GetOrganizationResponse(
            id,
            displayName!,
            registrationCountry!,
            organizationNumber!,
            status!,
            roles);
    }
}
