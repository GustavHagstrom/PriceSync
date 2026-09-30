namespace PriceSync.Modules.TradingRelationships.Features.End;

internal static class EndErrorCodes
{
    public const string TradingRelationshipNotFound = "trading-relationship-not-found";
    public const string ValidationFailed = "validation-failed";
    public const string Forbidden = "forbidden";
    public const string ActingOrganizationRequired = "acting-organization-required";
    public const string ActingOrganizationEnded = "acting-organization-ended";
    public const string ActingOrganizationNotFound = "acting-organization-not-found";
    public const string ActingOrganizationNotMemberOfTradingRelationship = "not-found";
}
