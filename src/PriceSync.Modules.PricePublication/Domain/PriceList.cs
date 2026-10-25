using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication.Contracts;

namespace PriceSync.Modules.PricePublication.Domain;

internal sealed class PriceList
{
    private PriceList() { }
    public PriceListId Id { get; private set; }
    public OrganizationId OwnerOrganizationId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public string Currency { get; private set; } = null!;
    public string Market { get; private set; } = null!;
    public OrganizationId? CustomerOrganizationId { get; private set; }

    public void Rename(string name) => Name = NormalizeName(name);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Name must contain 1–200 characters.", nameof(name));
        return name.Trim();
    }

    public static PriceList Create(OrganizationId owner, string name, string type, string currency, string market,
        OrganizationId? customer)
    {
        if (owner.Value == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(owner));
        var displayName = NormalizeName(name);
        if (type is not ("general" or "customerSpecificNet"))
            throw new ArgumentException("Unsupported Supplier Price List type.", nameof(type));
        if ((type == "general" && customer is not null) ||
            (type == "customerSpecificNet" && (customer is null || customer.Value.Value == Guid.Empty)))
            throw new ArgumentException("Only Customer-Specific Net lists require a Customer.", nameof(customer));
        if (currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Currency must be a three-letter uppercase code.", nameof(currency));
        if (string.IsNullOrWhiteSpace(market) || market.Trim().Length > 100)
            throw new ArgumentException("Market must contain 1–100 characters.", nameof(market));
        return new PriceList
        {
            Id = PriceListId.Create(), 
            OwnerOrganizationId = owner, 
            Name = displayName, 
            Type = type,
            Currency = currency, 
            Market = market.Trim(), 
            CustomerOrganizationId = customer,
        };
    }
}
