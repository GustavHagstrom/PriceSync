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

internal sealed class CreateHandler(AgreementsDbContext db, TimeProvider clock, IOptions<JsonOptions> jsonOptions,
    ICurrentPerson currentPerson, ICurrentOrganization currentOrganization,
    IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships)
{
    internal async Task<CreateResult> HandleAsync(CreateCommand command, CancellationToken cancellationToken)
    {
        if (currentOrganization.OrganizationId is not { } acting)
            return new CreateResult.ActingOrganizationRequired();
        var person = currentPerson.PersonId;
        if (!await authorizer.HasCapabilityAsync(person, acting, OrganizationCapability.PublishAgreements, cancellationToken))
            return new CreateResult.Forbidden();
        if (!Guid.TryParse(command.TradingRelationshipId, out var id) || id == Guid.Empty)
            return new CreateResult.InvalidRelationshipId();
        var relationshipId = TradingRelationshipId.FromGuid(id);
        var relationship = await relationships.GetAsync(relationshipId, cancellationToken);
        if (relationship is null || relationship.SupplierOrganizationId != acting)
            return new CreateResult.NotFound();
        if (relationship.Status == TradingRelationshipStatus.Ended)
            return new CreateResult.RelationshipEnded();
        if (command.IdempotencyKey is not { Length: >= 1 and <= 200 } key || key.Any(char.IsControl))
            return new CreateResult.InvalidIdempotencyKey();

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
            if (replay.TradingRelationshipId != relationshipId) return new CreateResult.KeyConflict();
            var result = ToCreatedResult(replay);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }

        var now = clock.GetUtcNow();
        var createdAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
        var agreement = PriceAgreement.Create(relationshipId, person, createdAt);
        db.PriceAgreements.Add(agreement);
        replay.Complete($"/api/v1/agreements/{agreement.Id}",
            JsonSerializer.Serialize(AgreementResponse.From(agreement), jsonOptions.Value.SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToCreatedResult(replay);
    }

    private static CreateResult.Created ToCreatedResult(AgreementCreationReplay replay)
    {
        if (replay.ResponseStatus is not { } status || replay.Location is not { } location || replay.ResponseBody is not { } body)
            throw new InvalidOperationException("An incomplete agreement creation replay cannot be returned.");
        return new CreateResult.Created(status, location, body);
    }
}
