using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Domain;

internal sealed record AgreementAcceptance(TradingRelationshipId TradingRelationshipId,
    AgreementVersionId VersionId, PersonId AcceptedByPersonId, DateTimeOffset AcceptedAt);
