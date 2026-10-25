namespace PriceSync.Modules.Agreements.Features.Create;

internal abstract record CreateResult
{
    // Both creation and matching retries return the original successful response.
    internal sealed record Created(int StatusCode, string Location, string ResponseBody) : CreateResult;
    internal sealed record KeyConflict : CreateResult;
}
