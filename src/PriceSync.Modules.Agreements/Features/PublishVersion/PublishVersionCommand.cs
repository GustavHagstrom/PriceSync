using System.Text.Json;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal sealed record PublishVersionCommand(Guid AgreementId, JsonElement ExpectedLatestVersionId,
    DateOnly? ValidFrom, DateOnly? ValidTo, CoverageRequest[]? Coverage, GroupDiscountRequest?[]? GroupDiscounts);
