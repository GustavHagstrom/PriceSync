namespace PriceSync.Modules.Agreements.Features.AcceptVersion;

internal sealed record AcceptVersionCommand(Guid AgreementId, Guid VersionId);
