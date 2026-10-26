using System.Globalization;

namespace PriceSync.Modules.Agreements.Features.List;

internal sealed class ListValidator
{
    internal Dictionary<string, string[]> Validate(ListQuery query, out int pageSize, out ListCursor? cursor)
    {
        var errors = new Dictionary<string, string[]>();
        pageSize = 50;
        cursor = null;
        if (query.PageSize is not null && (!int.TryParse(query.PageSize, NumberStyles.None,
                CultureInfo.InvariantCulture, out pageSize) || pageSize is < 1 or > 100))
            errors["pageSize"] = ["Page size must be an integer between 1 and 100."];
        if (query.Cursor is not null && !ListCursor.TryParse(query.Cursor, out cursor))
            errors["cursor"] = ["Cursor is invalid."];
        return errors;
    }
}
