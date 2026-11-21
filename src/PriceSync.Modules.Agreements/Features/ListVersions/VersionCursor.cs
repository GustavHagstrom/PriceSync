using System.Buffers.Binary;
using Microsoft.AspNetCore.WebUtilities;
using PriceSync.Modules.Agreements.Contracts;

namespace PriceSync.Modules.Agreements.Features.ListVersions;

internal static class VersionCursor
{
    internal static string Encode(AgreementId agreement, long ordinal)
    {
        var bytes = new byte[24];
        agreement.Value.TryWriteBytes(bytes);
        BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(16), ordinal);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    internal static bool TryParse(string value, AgreementId agreement, out long ordinal)
    {
        ordinal = 0;
        if (value.Length != 32) return false;
        byte[] bytes;
        try { bytes = WebEncoders.Base64UrlDecode(value); }
        catch (FormatException) { return false; }

        if (bytes.Length != 24 || new Guid(bytes.AsSpan(0, 16)) != agreement.Value) return false;
        ordinal = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(16));
        return ordinal > 0 && Encode(agreement, ordinal) == value;
    }
}
