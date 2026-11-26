using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Domain;

internal sealed record AgreementVersionWithdrawal(AgreementVersionId VersionId, string Reason,
    PersonId WithdrawnByPersonId, DateTimeOffset WithdrawnAt);
