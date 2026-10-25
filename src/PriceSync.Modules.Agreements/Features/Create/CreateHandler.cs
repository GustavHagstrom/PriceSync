using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.Create;

internal sealed class CreateHandler(AgreementsDbContext db, TimeProvider clock, IOptions<JsonOptions> jsonOptions)
{
    // Authority and relationship access must be checked by the caller before every invocation, including retries.
    internal async Task<AgreementCreationReplay?> HandleAsync(PersonId person, OrganizationId acting,
        TradingRelationshipId relationshipId, string key, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var operation = AgreementCreationReplay.CreateOperation;
        // The unique key arbitrates concurrent requests before an agreement is inserted.
        // At Read Committed a loser waits for the winner, then reads its completed response.
        var reserved = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO agreements.agreement_creation_replays
                ("PersonId", "ActingOrganizationId", "Operation", "Key", "TradingRelationshipId")
            VALUES ({person.Value}, {acting.Value}, {operation}, {key}, {relationshipId.Value})
            ON CONFLICT ON CONSTRAINT pk_agreement_creation_replays DO NOTHING
            """, cancellationToken);
        var replay = await db.CreationReplays.SingleAsync(x => x.PersonId == person &&
            x.ActingOrganizationId == acting && x.Operation == operation && x.Key == key, cancellationToken);
        if (reserved == 0)
        {
            if (replay.TradingRelationshipId != relationshipId) return null;
            if (replay.ResponseStatus is null || replay.Location is null || replay.ResponseBody is null)
                throw new InvalidOperationException("An incomplete agreement creation replay was committed.");
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var now = clock.GetUtcNow();
        var createdAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
        var agreement = PriceAgreement.Create(relationshipId, person, createdAt);
        db.PriceAgreements.Add(agreement);
        replay.Complete($"/api/v1/agreements/{agreement.Id}",
            JsonSerializer.Serialize(AgreementResponse.From(agreement), jsonOptions.Value.SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return replay;
    }
}
