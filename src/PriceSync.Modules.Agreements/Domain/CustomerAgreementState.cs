using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Domain;

internal sealed record CustomerAgreementState(TradingRelationshipId TradingRelationshipId, AgreementVersionId? ActiveVersionId, long Sequence);

internal sealed record CustomerAgreementTransition(TradingRelationshipId TradingRelationshipId, long Sequence, AgreementVersionId? FromVersionId, AgreementVersionId? ToVersionId, PersonId PersonId, DateTimeOffset DecidedAt);
