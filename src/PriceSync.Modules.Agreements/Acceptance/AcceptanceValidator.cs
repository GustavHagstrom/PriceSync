using PriceSync.Modules.Agreements.Contracts;

namespace PriceSync.Modules.Agreements.Acceptance;

internal static class AcceptanceValidator
{
    internal static (AgreementId AgreementId, AgreementVersionId VersionId)? Validate(Guid agreementId, Guid versionId) =>
        agreementId == Guid.Empty || versionId == Guid.Empty
            ? null
            : (new AgreementId(agreementId), new AgreementVersionId(versionId));
}
