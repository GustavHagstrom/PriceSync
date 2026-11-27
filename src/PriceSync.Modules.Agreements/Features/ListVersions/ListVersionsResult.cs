namespace PriceSync.Modules.Agreements.Features.ListVersions;

internal abstract record ListVersionsResult
{
    internal sealed record Success(ListVersionsResponse Response) : ListVersionsResult;
    internal sealed record ActingOrganizationRequired : ListVersionsResult;
    internal sealed record Forbidden : ListVersionsResult;
    internal sealed record NotFound : ListVersionsResult;
    internal sealed record ValidationFailed(IDictionary<string, string[]> Errors) : ListVersionsResult;
}
