namespace PriceSync.Modules.OrganizationAccess.Features.ListMyOrganizations;

internal sealed record ListMyOrganizationsResponse(
    Guid OrganizationId,
    string DisplayName,
    string RegistrationCountry,
    string OrganizationNumber,
    string Status,
    IReadOnlyCollection<string> Roles);
