namespace PriceSync.Modules.PricePublication.Features.Create;

internal sealed record CreateRequest(string? Name, string? Type, string? Currency, string? Market, Guid? CustomerOrganizationId);
