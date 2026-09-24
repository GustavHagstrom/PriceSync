using Microsoft.AspNetCore.Http;

namespace PriceSync.Modules.TradingRelationships.Features.End;

internal class EndEndpoints
{
    public async Task<IResult> HansleAsync(
        EndRequest request,
        EndHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request, cancellationToken);
        return Results.NoContent();
    }
}
