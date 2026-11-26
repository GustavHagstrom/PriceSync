using System.Globalization;

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
        if (knowledgeCutoff is not null && !TryParseCutoff(knowledgeCutoff, out cutoff))
            errors["knowledgeCutoff"] = ["Use an ISO 8601 timestamp with an explicit offset."];
        query = new(agreementId, date, cutoff.ToUniversalTime());
        return errors;
    }

    private static bool TryParseCutoff(string value, out DateTimeOffset cutoff) =>
        DateTimeOffset.TryParseExact(value,
            ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mm:ssK"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out cutoff)
        && (value.EndsWith('Z') || (value.Length >= 6 && value[^6] is '+' or '-'));
}
