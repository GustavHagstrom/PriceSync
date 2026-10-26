using System.Text.Json;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal sealed record PublishRequest(JsonElement ExpectedLatestVersionId, DateOnly? ValidFrom, DateOnly? ValidTo,
    CoverageRequest[]? Coverage, JsonElement GroupDiscounts);

internal sealed record CoverageRequest(Guid PriceListId, string? Type, decimal? GeneralDiscount);
