namespace PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

internal static class CreateTradingRelationshipErrorCodes
{
    public const string ActingOrganizationRequired = "acting_organization_required";
    public const string Forbidden = "forbidden";
    public const string SupplierNotFound = "supplier_not_found";
    public const string CustomerNotFound = "customer_not_found";
    public const string SupplierOrganizationEnded = "supplier_organization_ended";
    public const string CustomerOrganizationEnded = "customer_organization_ended";
    public const string ValidationFailed = "validation_failed";
}