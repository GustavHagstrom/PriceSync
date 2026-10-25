namespace PriceSync.Modules.Agreements.Features.Create;

internal sealed record CreateCommand(string? TradingRelationshipId, string? IdempotencyKey);
