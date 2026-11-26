using System.Text;

namespace PriceSync.Modules.Agreements.Features.WithdrawVersion;

internal sealed class WithdrawVersionValidator
{
    internal Dictionary<string, string[]>? Validate(WithdrawalRequest request, out string reason)
    {
        reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0 || reason.EnumerateRunes().Count() > 2000 || reason.Contains('\0'))
            return new() { ["reason"] = ["Enter a reason of 1–2000 characters without NUL."] };
        return null;
    }
}
