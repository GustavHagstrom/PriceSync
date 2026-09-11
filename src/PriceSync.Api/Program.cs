using PriceSync.Application;
using PriceSync.Modules.Agreements;
using PriceSync.Modules.CustomerPricing;
using PriceSync.Modules.IntegrationConfiguration;
using PriceSync.Modules.OrganizationAccess;
using PriceSync.Modules.PricePublication;
using PriceSync.Modules.RetrievalConsumption;
using PriceSync.Modules.TradingRelationships;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPriceSyncApplication();
builder.Services.AddOrganizationAccessModule(builder.Configuration);
builder.Services.AddTradingRelationshipsModule();
builder.Services.AddAgreementsModule();
builder.Services.AddPricePublicationModule();
builder.Services.AddCustomerPricingModule();
builder.Services.AddRetrievalConsumptionModule();
builder.Services.AddIntegrationConfigurationModule();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "PriceSync.Api" }));

app.Run();
