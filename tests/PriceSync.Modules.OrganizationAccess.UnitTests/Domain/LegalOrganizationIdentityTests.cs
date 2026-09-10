using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Domain;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.UnitTests.Domain;

public sealed class LegalOrganizationIdentityTests
{
    [Fact]
    public void Create_normalizes_swedish_identity_to_canonical_values()
    {
        var identity = LegalOrganizationIdentity.Create(" se ", "556016-0680");

        identity.RegistrationCountry.Should().Be("SE");
        identity.NationalOrganizationNumber.Should().Be("5560160680");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_rejects_missing_registration_country(string? registrationCountry)
    {
        var create = () => LegalOrganizationIdentity.Create(registrationCountry, "5560160680");

        create.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_rejects_missing_national_organization_number(string? nationalOrganizationNumber)
    {
        var create = () => LegalOrganizationIdentity.Create("SE", nationalOrganizationNumber);

        create.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("NO")]
    [InlineData("DK")]
    [InlineData("US")]
    public void Create_rejects_unsupported_registration_countries(string registrationCountry)
    {
        var create = () => LegalOrganizationIdentity.Create(registrationCountry, "5560160680");

        create.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("5560160680", "5560160680")]
    [InlineData("556016-0680", "5560160680")]
    public void Create_accepts_plain_and_hyphenated_swedish_organization_numbers(
        string input,
        string expectedCanonicalNumber)
    {
        var identity = LegalOrganizationIdentity.Create("SE", input);

        identity.NationalOrganizationNumber.Should().Be(expectedCanonicalNumber);
    }

    [Theory]
    [InlineData("556016 0680")]
    [InlineData("556016/0680")]
    [InlineData("55601-60680")]
    [InlineData("165560160680")]
    [InlineData("556016068")]
    [InlineData("55601606800")]
    [InlineData("556016068A")]
    [InlineData("556016068\u0660")]
    public void Create_rejects_unsupported_swedish_organization_number_formats(string nationalOrganizationNumber)
    {
        var create = () => LegalOrganizationIdentity.Create("SE", nationalOrganizationNumber);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_swedish_organization_numbers_with_invalid_check_digit()
    {
        var create = () => LegalOrganizationIdentity.Create("SE", "5560160681");

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_swedish_numbers_where_third_digit_is_less_than_two()
    {
        var create = () => LegalOrganizationIdentity.Create("SE", "5500160685");

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equalities_use_canonical_country_and_organization_number()
    {
        var plain = LegalOrganizationIdentity.Create("SE", "5560160680");
        var hyphenated = LegalOrganizationIdentity.Create(" se ", "556016-0680");

        plain.Should().Be(hyphenated);
    }
}
