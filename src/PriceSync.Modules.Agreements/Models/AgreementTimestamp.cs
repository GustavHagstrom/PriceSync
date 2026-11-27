using System.Globalization;

namespace PriceSync.Modules.Agreements.Models;

internal static class AgreementTimestamp
{
    internal static bool TryParse(string value, out DateTimeOffset timestamp) =>
        DateTimeOffset.TryParseExact(value,
            ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mm:ssK"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp)
        && (value.EndsWith('Z') || (value.Length >= 6 && value[^6] is '+' or '-'));
}
