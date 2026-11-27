namespace PriceSync.Modules.Agreements.Features.List;

internal sealed record ListQuery(string? TradingRelationshipId, string? PageSize, string? Cursor);
