namespace PriceSync.Modules.TradingRelationships.Features.Create;

internal class CreateValidator
{
    public ValidationErrors Validate(CreateRequest request)
    {
        var errors = new ValidationErrors();

        if (request.SupplierOrganizationId == request.CustomerOrganizationId)
        {
            errors.Add(nameof(request.CustomerOrganizationId), "You cannot create a trading relationship with the same organization.");
        }

        if (request.SupplierOrganizationId == Guid.Empty)
        {
            errors.Add(nameof(request.SupplierOrganizationId), "Supplier organization ID cannot be empty.");
        }

        if (request.CustomerOrganizationId == Guid.Empty)
        {
            errors.Add(nameof(request.CustomerOrganizationId), "Customer organization ID cannot be empty.");
        }

        return errors;
    }
}
