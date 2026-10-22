using Microsoft.AspNetCore.WebUtilities;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using System.Globalization;
using System.Text;

namespace PriceSync.Modules.TradingRelationships.Models;

internal sealed record class ListKeyset
{
    public ListKeyset(DateTimeOffset createdAt, TradingRelationshipId id)
    {
        CreatedAt = createdAt;
        Id = id;
    }

    public DateTimeOffset CreatedAt { get; }
    public TradingRelationshipId Id { get; }
    public string Cursor => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes($"{CreatedAt:o}|{Id}"));

    public static ListKeyset FromCursor(string cursor)
    {
        string decodedCursor;
        try
        {
            decodedCursor = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor));
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid cursor format.", nameof(cursor));
        }

        var parts = decodedCursor.Split('|');
        if (parts.Length != 2)
        {
            throw new ArgumentException("Invalid cursor format.", nameof(cursor));
        }
        var createdAt = DateTimeOffset.TryParseExact(parts[0], "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedCreatedAt) ? parsedCreatedAt : throw new ArgumentException("Invalid cursor format.", nameof(cursor));
        var guid = Guid.TryParse(parts[1], out var parsedGuid) ? parsedGuid : throw new ArgumentException("Invalid cursor format.", nameof(cursor));
        var id = TradingRelationshipId.FromGuid(guid);
        return new ListKeyset(createdAt, id);
    }
    public static ListKeyset FromTradingRelationship(TradingRelationship relationship)
    {
        return new ListKeyset(relationship.CreatedAt, relationship.Id);
    }
}
