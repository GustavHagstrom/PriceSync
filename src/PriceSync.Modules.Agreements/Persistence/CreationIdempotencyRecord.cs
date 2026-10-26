using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence;

internal sealed class CreationIdempotencyRecord
{
    internal const string CreateOperation = "CreateAgreement";
    public PersonId PersonId { get; private set; }
    public OrganizationId ActingOrganizationId { get; private set; }
    public string Operation { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public TradingRelationshipId TradingRelationshipId { get; private set; }
    public short? ResponseStatus { get; private set; }
    public string? Location { get; private set; }
    public string? ResponseBody { get; private set; }

    internal void Complete(string location, string responseBody)
    {
        ResponseStatus = 201;
        Location = location;
        ResponseBody = responseBody;
    }
}
