using PriceSync.Modules.Agreements.Models;
namespace PriceSync.Modules.Agreements.Features.Get;

internal abstract record GetResult
{
    internal sealed record Success(AgreementResponse Response) : GetResult;
    internal sealed record ActingOrganizationRequired : GetResult;
    internal sealed record Forbidden : GetResult;
    internal sealed record NotFound : GetResult;
}
