namespace PriceSync.Modules.OrganizationAccess.Domain;

internal sealed record RegistrationCountry
{
    private RegistrationCountry(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public static RegistrationCountry Create(string? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var code = value.Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            throw new ArgumentException("Registration country is required.", nameof(value));
        }

        if (code != "SE")
        {
            throw new ArgumentException("Only Swedish registration country SE is supported.", nameof(value));
        }

        return new RegistrationCountry(code);
    }

    public override string ToString() => Code;
}
