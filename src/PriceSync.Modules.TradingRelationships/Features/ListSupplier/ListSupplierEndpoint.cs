using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.TradingRelationships.Infrastructure;
using PriceSync.Modules.TradingRelationships.Models;
namespace PriceSync.Modules.TradingRelationships.Features.ListSupplier;

internal class ListSupplierEndpoint
{
    public static async Task<IResult> HandleAsync(
        [FromQuery] ListStatusFilter? status,
        [FromQuery] string? cursor,
        ILogger<ListSupplierEndpoint> logger,
        ListSupplierHandler handler,
        CancellationToken cancellationToken)
    {
        using var scope = LogScope.BeginScope(logger, "ListSupplier", tradingRelationshipId: null, personId: null, organizationId: null);
        ListStatusFilter filter = status ?? ListStatusFilter.Active;
        ListKeyset? keyset;
        if (cursor is not null)
        {
            try
            {
                keyset = ListKeyset.FromCursor(cursor);
            }
            catch (ArgumentException)
            {
                logger.LogInformation("Invalid cursor '{Cursor}' provided", cursor);
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid cursor",
                    detail: $"The cursor '{cursor}' is not valid.",
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", ListSupplierErrorCodes.InvalidCursor }
                    });
            }
        }

        throw new NotImplementedException();
    }
}

