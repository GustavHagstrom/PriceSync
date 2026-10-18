using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.TradingRelationships.Infrastructure;
using PriceSync.Modules.TradingRelationships.Models;
namespace PriceSync.Modules.TradingRelationships.Features.ListSupplier;

internal class ListSupplierEndpoint
{
    public static async Task<IResult> HandleAsync(
        [FromQuery] string? status,
        [FromQuery] string? cursor,
        ILogger<ListSupplierEndpoint> logger,
        ListSupplierHandler handler,
        CancellationToken cancellationToken)
    {
        using var scope = LogScope.BeginScope(logger, "ListSupplier", tradingRelationshipId: null, personId: null, organizationId: null);
        status = status ?? ListStatusFilter.Active.ToString();
        if (!Enum.TryParse<ListStatusFilter>(status, ignoreCase: true, out var parsedStatus))
        {
            logger.LogInformation("Invalid status '{Status}' provided", status);
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid status",
                detail: $"The status '{status}' is not valid.",
                extensions: new Dictionary<string, object?>
                {
                    { "code", ListSupplierErrorCodes.InvalidStatus }
                });
        }

        ListKeyset? keyset = null;
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
        var query = new ListSupplierQuery(parsedStatus, keyset);
        var result = await handler.HandleAsync(query, cancellationToken);
        var response = result switch
        {
            ListSupplierHandlerResult.Succes succes => Results.Ok(succes.Response),
            _ => Results.InternalServerError()
        };


        throw new NotImplementedException();
    }
}

