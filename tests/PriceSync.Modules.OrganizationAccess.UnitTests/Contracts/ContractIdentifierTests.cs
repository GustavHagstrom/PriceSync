using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.UnitTests.Contracts;

public sealed class ContractIdentifierTests
{
    [Fact]
    public void Organization_access_contract_identifiers_reject_empty_guids()
    {
        var createOrganizationId = () => new OrganizationId(Guid.Empty);
        var createPersonId = () => new PersonId(Guid.Empty);

        createOrganizationId.Should().Throw<ArgumentException>();
        createPersonId.Should().Throw<ArgumentException>();
    }
}
