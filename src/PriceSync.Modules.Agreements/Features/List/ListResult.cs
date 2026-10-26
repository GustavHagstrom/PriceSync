namespace PriceSync.Modules.Agreements.Features.List;

internal abstract record ListResult
{
    internal sealed record Success(ListResponse Response) : ListResult;
    internal sealed record ActingOrganizationRequired : ListResult;
    internal sealed record Forbidden : ListResult;
    internal sealed record NotFound : ListResult;
    internal sealed record ValidationFailed(IDictionary<string, string[]> Errors) : ListResult;
}
