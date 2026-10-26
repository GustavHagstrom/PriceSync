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
    public AgreementVersionId? LatestVersionId { get; private set; }
    public long LatestOrdinal { get; private set; }

    internal void PublishFirst(PriceAgreementVersion version)
    {
        if (LatestVersionId is not null || version.AgreementId != Id || version.TradingRelationshipId != TradingRelationshipId)
            throw new InvalidOperationException("Version must be the first publication in this Agreement scope.");
        LatestVersionId = version.Id;
        LatestOrdinal = version.Ordinal;
    }

    private PriceAgreement() { }

    internal static PriceAgreement Create(TradingRelationshipId relationshipId, PersonId personId, DateTimeOffset createdAt)
    {
        if (relationshipId.Value == Guid.Empty) throw new ArgumentException("Relationship is required.", nameof(relationshipId));
        if (personId.Value == Guid.Empty) throw new ArgumentException("Person is required.", nameof(personId));
        return new PriceAgreement
        {
            Id = AgreementId.Create(), TradingRelationshipId = relationshipId,
            CreatedByPersonId = personId, CreatedAt = createdAt,
        };
    }
}
