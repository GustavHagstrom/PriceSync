using PriceSync.Modules.Agreements.Domain;

namespace PriceSync.Modules.Agreements.Acceptance;

internal abstract record AcceptanceResult
{
    internal sealed record Success(AgreementAcceptance? Acceptance) : AcceptanceResult;
    internal sealed record ActingOrganizationRequired : AcceptanceResult;
    internal sealed record Forbidden : AcceptanceResult;
    internal sealed record NotFound : AcceptanceResult;
    internal sealed record RelationshipEnded : AcceptanceResult;
}
