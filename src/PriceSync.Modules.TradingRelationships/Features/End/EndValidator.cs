using PriceSync.BuildingBlocks.Validation;

namespace PriceSync.Modules.TradingRelationships.Features.End;

internal class EndValidator
{
    public ValidationErrorCollection Validate(EndCommand command)
    {
        var errors = new ValidationErrorCollection();
        if (command.TradingRelationshipId.Value == Guid.Empty)
        {
            errors.Add(nameof(command.TradingRelationshipId), "TradingRelationshipId cannot be empty.");
        }
        return errors;
    }
}
