using PriceSync.BuildingBlocks.Validation;

namespace PriceSync.Modules.TradingRelationships.Features.Create;

internal class CreateValidator
{
    public ValidationErrorCollection Validate(CreateCommand command)
    {
        var errors = new ValidationErrorCollection();

        if (command.SupplierOrganizationId == command.CustomerOrganizationId)
        {
            errors.Add(nameof(command.CustomerOrganizationId), "You cannot create a trading relationship with the same organization.");
        }

        if (command.SupplierOrganizationId.Value == Guid.Empty)
        {
            errors.Add(nameof(command.SupplierOrganizationId), "Supplier organization ID cannot be empty.");
        }

        if (command.CustomerOrganizationId.Value == Guid.Empty)
        {
            errors.Add(nameof(command.CustomerOrganizationId), "Customer organization ID cannot be empty.");
        }

        return errors;
    }
}
