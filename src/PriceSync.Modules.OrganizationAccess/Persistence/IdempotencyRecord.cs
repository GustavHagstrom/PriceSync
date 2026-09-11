using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.OrganizationAccess.Persistence;

internal sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
        Operation = null!;
        IdempotencyKey = null!;
        RequestHash = [];
        ResponseBody = "{}";
    }

    public IdempotencyRecord(
        PersonId personId,
        string operation,
        string idempotencyKey,
        byte[] requestHash,
        short responseStatus,
        string responseBody,
        DateTimeOffset createdAt)
    {
        PersonId = personId;
        Operation = operation;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        ResponseStatus = responseStatus;
        ResponseBody = responseBody;
        CreatedAt = createdAt;
    }

    public PersonId PersonId { get; private set; }

    public string Operation { get; private set; }

    public string IdempotencyKey { get; private set; }

    public byte[] RequestHash { get; private set; }

    public short ResponseStatus { get; private set; }

    public string ResponseBody { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
