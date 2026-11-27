using PriceSync.Modules.Agreements.Models;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal abstract record PublishVersionResult
{
    internal sealed record Created(string Location, VersionResponse Response) : PublishVersionResult;
    internal sealed record Existing(VersionResponse Response) : PublishVersionResult;
    internal sealed record ListVerificationUnavailable : PublishVersionResult;
    internal sealed record ActingOrganizationRequired : PublishVersionResult;
    internal sealed record Forbidden : PublishVersionResult;
    internal sealed record NotFound : PublishVersionResult;
    internal sealed record RelationshipEnded : PublishVersionResult;
    internal sealed record PublicationConflict : PublishVersionResult;
    internal sealed record ValidationFailed(IDictionary<string, string[]> Errors) : PublishVersionResult;
}
