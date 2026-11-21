using PriceSync.Modules.Agreements.Models;

namespace PriceSync.Modules.Agreements.Features.ListVersions;

internal sealed record ListVersionsResponse(IReadOnlyList<VersionResponse> Items, string? NextCursor);
