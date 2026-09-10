namespace PriceSync.Modules.OrganizationAccess.Domain;

internal sealed record LegalOrganizationIdentity
{
    private LegalOrganizationIdentity(RegistrationCountry registrationCountry, SwedishOrganizationNumber organizationNumber)
    {
        RegistrationCountry = registrationCountry.Code;
        NationalOrganizationNumber = organizationNumber.Value;
    }

    public string RegistrationCountry { get; }

    public string NationalOrganizationNumber { get; }

    public static LegalOrganizationIdentity Create(string? registrationCountry, string? nationalOrganizationNumber)
    {
        var country = Domain.RegistrationCountry.Create(registrationCountry);
        var organizationNumber = SwedishOrganizationNumber.Create(nationalOrganizationNumber);

        return new LegalOrganizationIdentity(country, organizationNumber);
    }
}
