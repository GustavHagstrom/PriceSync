using PriceSync.BuildingBlocks.Validation;
using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.ListCustomer;

internal class ListCustomerValidator
{
    public ValidationErrorCollection Validate(string? status, string? cursor, out ListCustomerQuery query)
    {
        status = status?.ToLowerInvariant() ?? "active";
        ListKeyset? keyset = null;
        ListStatusFilter filter = ListStatusFilter.Active;
        var errors = new ValidationErrorCollection();


        if (IsAcceptedStatusString(status) && Enum.TryParse<ListStatusFilter>(status, ignoreCase: true, out var parsedFilter))
        {
            filter = parsedFilter;
        }
        else 
        {
            errors.Add("status", $"The status '{status}' is not valid.");
        }

        
        if (cursor is not null)
        {
            try
            {
                keyset = ListKeyset.FromCursor(cursor);
            }
            catch (ArgumentException ex)
            {
                errors.Add("cursor", $"The cursor '{cursor}' is not valid. {ex.Message}");
            }
        }

        query = new ListCustomerQuery(filter, keyset);
        return errors;
    }
    private bool IsAcceptedStatusString(string status)
    {
        if(status == ListStatusFilter.Active.ToString().ToLowerInvariant() ||
            status == ListStatusFilter.Ended.ToString().ToLowerInvariant() ||
            status == ListStatusFilter.All.ToString().ToLowerInvariant())
            return true;
        return false;
    }
}


