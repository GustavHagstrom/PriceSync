using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using PriceSync.Modules.Agreements.Contracts;

namespace PriceSync.Modules.Agreements.Features.List;

internal sealed record ListCursor(DateTimeOffset CreatedAt, AgreementId Id)
{
    internal string Encode() => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
        $"{CreatedAt.ToString("O", CultureInfo.InvariantCulture)}|{Id}"));

    internal static bool TryParse(string value, out ListCursor? cursor)
    {
        cursor = null;
        if (value.Length is 0 or > 256) return false;
        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)); }
        catch (FormatException) { return false; }
        var parts = decoded.Split('|');
        if (parts.Length != 2 || !DateTimeOffset.TryParseExact(parts[0], "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var createdAt) || createdAt.Offset != TimeSpan.Zero || createdAt.Ticks % 10 != 0
            || !Guid.TryParseExact(parts[1], "D", out var id) || id == Guid.Empty)
            return false;
        cursor = new ListCursor(createdAt, new AgreementId(id));
        return true;
    }
}
