namespace PriceSync.Modules.Agreements.Features.List;

internal sealed record ListResponse(IReadOnlyList<AgreementResponse> Items, string? NextCursor);
