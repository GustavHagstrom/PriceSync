namespace PriceSync.Modules.Agreements.Features.ListVersions;

internal sealed record ListVersionsQuery(Guid AgreementId, string? PageSize, string? Cursor);
