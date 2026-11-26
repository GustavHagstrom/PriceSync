namespace PriceSync.Modules.Agreements.Features.WithdrawVersion;

internal sealed record WithdrawVersionCommand(Guid AgreementId, Guid VersionId, string Reason);
