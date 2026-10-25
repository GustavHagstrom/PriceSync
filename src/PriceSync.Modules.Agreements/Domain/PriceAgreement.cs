using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Domain;

internal sealed class PriceAgreement
{
    public AgreementId Id { get; private set; }
    public TradingRelationshipId TradingRelationshipId { get; private set; }
    public PersonId CreatedByPersonId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PriceAgreement() { }

    internal static PriceAgreement Create(TradingRelationshipId relationshipId, PersonId personId, TimeProvider clock)
    {
        if (relationshipId.Value == Guid.Empty) throw new ArgumentException("Relationship is required.", nameof(relationshipId));
        if (personId.Value == Guid.Empty) throw new ArgumentException("Person is required.", nameof(personId));
        var now = clock.GetUtcNow();
        // PostgreSQL timestamps preserve microseconds; use the same value in the initial response.
        var createdAt = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
        return new PriceAgreement
        {
            Id = AgreementId.Create(), TradingRelationshipId = relationshipId,
            CreatedByPersonId = personId, CreatedAt = createdAt,
        };
    }
}
