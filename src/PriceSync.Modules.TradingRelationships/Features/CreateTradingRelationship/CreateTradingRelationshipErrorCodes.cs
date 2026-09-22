namespace PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

internal static class CreateTradingRelationshipErrorCodes
{
    public const string MissingCurrentOrganization = "MissingCurrentOrganization";
    public const string Forbidden = "Forbidden";
    public const string SupplierNotFound = "SupplierNotFound";
    public const string CustomerNotFound = "CustomerNotFound";
    public const string SupplierOrganizationEnded = "SupplierOrganizationEnded";
    public const string CustomerOrganizationEnded = "CustomerOrganizationEnded";
    public const string ValidationFailed = "ValidationFailed";
}