using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;

namespace PriceSync.Modules.TradingRelationships.UnitTests.Domain;

public sealed class TradingRelationshipTests
{
    [Fact]
    public void Create_starts_an_active_directed_relationship()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var createdAt = new DateTimeOffset(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);

        var relationship = TradingRelationship.Create(supplierId, customerId, createdAt);

        relationship.Id.Value.Should().NotBeEmpty();
        relationship.SupplierOrganizationId.Should().Be(supplierId);
        relationship.CustomerOrganizationId.Should().Be(customerId);
        relationship.Status.Should().Be(TradingRelationshipStatus.Active);
        relationship.CreatedAt.Should().Be(createdAt);
        relationship.EndedAt.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_the_same_organization_as_both_parties()
    {
        var organizationId = NewOrganizationId();

        var create = () => TradingRelationship.Create(
            organizationId,
            organizationId,
            DateTimeOffset.UtcNow);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_an_empty_supplier_id()
    {
        var create = () => TradingRelationship.Create(
            default,
            NewOrganizationId(),
            DateTimeOffset.UtcNow);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_an_empty_customer_id()
    {
        var create = () => TradingRelationship.Create(
            NewOrganizationId(),
            default,
            DateTimeOffset.UtcNow);

        create.Should().Throw<ArgumentException>();
    }

    private static OrganizationId NewOrganizationId() => new(Guid.CreateVersion7());
}
