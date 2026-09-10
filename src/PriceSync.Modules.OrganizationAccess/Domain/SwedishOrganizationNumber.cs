namespace PriceSync.Modules.OrganizationAccess.Domain;

internal sealed record SwedishOrganizationNumber
{
    private SwedishOrganizationNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SwedishOrganizationNumber Create(string? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!HasAcceptedFormat(value))
        {
            throw new ArgumentException("Swedish organization number must use ten digits or the ######-#### format.", nameof(value));
        }

        var canonical = value.Length == 11
            ? string.Concat(value.AsSpan(0, 6), value.AsSpan(7, 4))
            : value;

        if (canonical[2] < '2')
        {
            throw new ArgumentException("Swedish organization number must have a third digit of at least 2.", nameof(value));
        }

        if (!HasValidCheckDigit(canonical))
        {
            throw new ArgumentException("Swedish organization number has an invalid check digit.", nameof(value));
        }

        return new SwedishOrganizationNumber(canonical);
    }

    public override string ToString() => Value;

    private static bool HasAcceptedFormat(string value)
    {
        if (value.Length == 10)
        {
            return value.All(IsAsciiDigit);
        }

        if (value.Length != 11 || value[6] != '-')
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (index == 6)
            {
                continue;
            }

            if (!IsAsciiDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static bool HasValidCheckDigit(string value)
    {
        var sum = 0;

        for (var index = 0; index < 9; index++)
        {
            var digit = value[index] - '0';
            var product = digit * (index % 2 == 0 ? 2 : 1);
            sum += product / 10 + product % 10;
        }

        var expectedCheckDigit = (10 - sum % 10) % 10;
        var actualCheckDigit = value[9] - '0';

        return actualCheckDigit == expectedCheckDigit;
    }
}
