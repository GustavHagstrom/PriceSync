using System.Globalization;
using System.Text.Json;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal static class PublicationDiscount
{
    internal static bool TryRead(JsonElement input, out decimal discount)
    {
        discount = default;
        if (input.ValueKind != JsonValueKind.Number) return false;

        // A decimal conversion can round or underflow, so check the original number's scale first.
        var number = input.GetRawText().AsSpan();
        var exponentIndex = number.IndexOfAny('e', 'E');
        var significand = exponentIndex < 0 ? number : number[..exponentIndex];
        var lastNonZero = significand.LastIndexOfAnyInRange('1', '9');
        if (lastNonZero < 0) return true;

        var exponent = 0;
        if (exponentIndex >= 0 && !int.TryParse(number[(exponentIndex + 1)..],
                NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            return false;

        var point = significand.IndexOf('.');
        var scale = point < 0 ? lastNonZero - significand.Length + 1
            : lastNonZero > point ? lastNonZero - point : lastNonZero - point + 1;
        return (long)scale - exponent <= 1 &&
            input.TryGetDecimal(out discount) && discount is >= 0 and <= 100;
    }
}
