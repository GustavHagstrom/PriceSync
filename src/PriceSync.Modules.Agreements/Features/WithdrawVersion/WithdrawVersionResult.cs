using PriceSync.Modules.Agreements.Models;

namespace PriceSync.Modules.Agreements.Features.WithdrawVersion;

internal abstract record WithdrawVersionResult
{
    internal sealed record Success(WithdrawalResponse Response) : WithdrawVersionResult;
    internal sealed record ActingOrganizationRequired : WithdrawVersionResult;
    internal sealed record Forbidden : WithdrawVersionResult;
    internal sealed record NotFound : WithdrawVersionResult;
    internal sealed record ReasonConflict : WithdrawVersionResult;
}
