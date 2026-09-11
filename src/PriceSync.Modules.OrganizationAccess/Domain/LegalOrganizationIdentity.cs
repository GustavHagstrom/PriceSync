namespace PriceSync.Modules.OrganizationAccess.Domain;

internal sealed record LegalOrganizationIdentity
{
    private LegalOrganizationIdentity(RegistrationCountry registrationCountry, SwedishOrganizationNumber organizationNumber)
    {
        RegistrationCountry = registrationCountry.Code;
        NationalOrganizationNumber = organizationNumber.Value;
    }

    private LegalOrganizationIdentity()
    {
        RegistrationCountry = null!;
        NationalOrganizationNumber = null!;
    }

    public string RegistrationCountry { get; private set; }

    public string NationalOrganizationNumber { get; private set; }

    public static LegalOrganizationIdentity Create(string? registrationCountry, string? nationalOrganizationNumber)
    {
        var country = Domain.RegistrationCountry.Create(registrationCountry);
        var organizationNumber = SwedishOrganizationNumber.Create(nationalOrganizationNumber);

        return new LegalOrganizationIdentity(country, organizationNumber);
    }
}
