using PriceSync.Modules.TradingRelationships.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.TradingRelationships.Features.Create;
using PriceSync.Modules.TradingRelationships.Features.End;
using PriceSync.Modules.TradingRelationships.Features.Get;
using PriceSync.Modules.TradingRelationships.Features.ListSupplier;
using PriceSync.Modules.TradingRelationships.Features.ListCustomer;

namespace PriceSync.Modules.TradingRelationships;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapTradingRelationshipsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints
            .MapGroup(Routes.BaseV1)
            .WithTags("TradingRelationships")
            .RequireAuthorization();

        api.MapPost("", CreateEndpoint.HandleAsync)
            .WithName(Routes.Names.Create)
            .WithSummary("Create a trading relationship")
            .WithDescription("Links an active supplier and an active customer. The acting organization must be one of the parties. If an active relationship already exists for that pair, the operation returns it.");
        api.MapPost("{tradingRelationshipId:guid}/end", EndEndpoint.HandleAsync)
            .WithName(Routes.Names.End)
            .WithSummary("End a trading relationship")
            .WithDescription("Marks the relationship as ended and records when it ended. Repeating the operation returns the existing end timestamp.");
        api.MapGet("{tradingRelationshipId:guid}", GetEndpoint.HandleAsync)
            .WithName(Routes.Names.Get)
            .WithSummary("Get a trading relationship")
            .WithDescription("Returns the supplier, customer, and lifecycle status of the relationship by ID when the acting organization is a party.");
        api.MapGet("/supplier", ListSupplierEndpoint.HandleAsync)
            .WithName(Routes.Names.ListSuppliers)
            .WithSummary("List relationships as the supplier")
            .WithDescription("Returns a page of trading relationships where the acting organization is the supplier. The `status` filter accepts `active`, `ended`, or `all` and defaults to `active`.");

        api.MapGet("/customer", ListCustomerEndpoint.HandleAsync)
            .WithName(Routes.Names.ListCustomers)
            .WithSummary("List relationships as the customer")
            .WithDescription("Returns a page of trading relationships where the acting organization is the customer. The `status` filter accepts `active`, `ended`, or `all` and defaults to `active`.");

        return endpoints;
    }
}
