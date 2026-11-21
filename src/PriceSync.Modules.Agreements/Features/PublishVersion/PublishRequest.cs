using System.Text.Json;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal sealed record PublishRequest(JsonElement ExpectedLatestVersionId, DateOnly? ValidFrom, DateOnly? ValidTo,
    CoverageRequest[]? Coverage, GroupDiscountRequest?[]? GroupDiscounts);

internal sealed record CoverageRequest(Guid PriceListId, string? Type, JsonElement GeneralDiscount);

internal sealed record GroupDiscountRequest(string? Code, JsonElement Discount);
