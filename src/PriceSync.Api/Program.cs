using PriceSync.Application;
using PriceSync.Api.Authentication;
using PriceSync.Modules.Agreements;
using PriceSync.Modules.CustomerPricing;
using PriceSync.Modules.IntegrationConfiguration;
using PriceSync.Modules.OrganizationAccess;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication;
using PriceSync.Modules.RetrievalConsumption;
using PriceSync.Modules.TradingRelationships;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPriceSyncApplication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddScoped<ICurrentPerson, ClaimsCurrentPerson>();
builder.Services.AddOrganizationAccessModule(builder.Configuration);
builder.Services.AddTradingRelationshipsModule();
builder.Services.AddAgreementsModule();
builder.Services.AddPricePublicationModule();
builder.Services.AddCustomerPricingModule();
builder.Services.AddRetrievalConsumptionModule();
builder.Services.AddIntegrationConfigurationModule();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "PriceSync.Api" }));
app.MapOrganizationAccessEndpoints();

app.Run();

public partial class Program;
