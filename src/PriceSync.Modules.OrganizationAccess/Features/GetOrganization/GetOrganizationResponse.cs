namespace PriceSync.Modules.OrganizationAccess.Features.GetOrganization;

internal sealed record GetOrganizationResponse(
    Guid OrganizationId,
    string DisplayName,
    string RegistrationCountry,
    string OrganizationNumber,
    string Status,
    IReadOnlyCollection<string> Roles);
