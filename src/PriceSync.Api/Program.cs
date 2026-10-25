using Microsoft.AspNetCore.Authentication;
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
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(UnavailableAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, UnavailableAuthenticationHandler>(
        UnavailableAuthenticationHandler.SchemeName,
        options => { });
builder.Services.AddAuthorization();
builder.Services.AddScoped<ICurrentPerson, ClaimsCurrentPerson>();
builder.Services.AddScoped<ICurrentOrganization, CurrentOrganization>();
builder.Services.AddOrganizationAccessModule(builder.Configuration);
builder.Services.AddTradingRelationshipsModule(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAgreementsModule();
builder.Services.AddPricePublicationModule(builder.Configuration);
builder.Services.AddCustomerPricingModule();
builder.Services.AddRetrievalConsumptionModule();
builder.Services.AddIntegrationConfigurationModule();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(exceptionHandler =>
{
    exceptionHandler.Run(async httpContext =>
    {
        await Results.Problem(
                title: "An unexpected error occurred.",
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "internal_server_error",
                })
            .ExecuteAsync(httpContext);
    });
});

app.UseStatusCodePages(async statusCodeContext =>
{
    if (statusCodeContext.HttpContext.Response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        await Results.Problem(
                title: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "authentication_required",
                })
            .ExecuteAsync(statusCodeContext.HttpContext);
    }
});
app.UseAuthentication();
app.UseAuthorization();

app.MapOrganizationAccessEndpoints();
app.MapTradingRelationshipsEndpoints();
app.MapPricePublicationEndpoints();

app.Run();

public partial class Program;
