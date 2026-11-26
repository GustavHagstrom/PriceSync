using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Features.WithdrawVersion;

internal sealed record WithdrawalRequest(string? Reason);

internal static class WithdrawVersionEndpoint
{
    internal static async Task<IResult> HandleAsync(Guid agreementId, Guid versionId, WithdrawalRequest request,
        WithdrawVersionValidator validator, WithdrawVersionHandler handler, CancellationToken cancellationToken)
    {
        if (validator.Validate(request, out var reason) is { } errors)
            return Results.ValidationProblem(errors,
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });

        var result = await handler.HandleAsync(new WithdrawVersionCommand(agreementId, versionId, reason),
            cancellationToken);
        return result switch
        {
            WithdrawVersionResult.Success success => Results.Ok(success.Response),
            WithdrawVersionResult.ActingOrganizationRequired => Problem(400, OrganizationContextErrors.Required),
            WithdrawVersionResult.Forbidden => Problem(403, "forbidden"),
            WithdrawVersionResult.NotFound => Problem(404, "agreement_version_not_found"),
            WithdrawVersionResult.ReasonConflict => Problem(409, "agreement_withdrawal_reason_conflict"),
            _ => throw new InvalidOperationException("Unexpected result type."),
        };
    }

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
