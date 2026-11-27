using System.Globalization;
using PriceSync.Modules.Agreements.Models;

namespace PriceSync.Modules.Agreements.Features.Recommendation;

internal sealed class RecommendationValidator(TimeProvider clock)
{
    internal Dictionary<string, string[]> Validate(Guid agreementId, string? evaluatedDate, string? knowledgeCutoff,
        out RecommendationQuery query)
    {
        var now = clock.GetUtcNow();
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        var cutoff = now;
        var errors = new Dictionary<string, string[]>();
        if (evaluatedDate is not null && !DateOnly.TryParseExact(evaluatedDate, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            errors["evaluatedDate"] = ["Use a date in yyyy-MM-dd format."];
        if (knowledgeCutoff is not null && !AgreementTimestamp.TryParse(knowledgeCutoff, out cutoff))
            errors["knowledgeCutoff"] = ["Use an ISO 8601 timestamp with an explicit offset."];
        query = new(agreementId, date, cutoff.ToUniversalTime());
        return errors;
    }
}
