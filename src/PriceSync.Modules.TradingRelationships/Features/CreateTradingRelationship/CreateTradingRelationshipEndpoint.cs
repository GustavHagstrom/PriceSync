using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.TradingRelationships.Features.CreateTradingRelationship;

internal class CreateTradingRelationshipEndpoint
{
    internal static async Task<IResult> HandleAsync(
        CreateTradingRelationshipCommand request,
        CreateTradingRelationshipHandler handler,
        CreateTradingRelationshipValidator validator,
        CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.HasErrors)
        {
            return Results.ValidationProblem(errors.GetErrors());
        }

        var result = await handler.HandleAsync(request, cancellationToken);
        return Results.Ok();
        
    }
}
