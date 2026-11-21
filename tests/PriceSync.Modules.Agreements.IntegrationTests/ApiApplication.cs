using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.IntegrationTests;

internal sealed class ApiApplication(string connectionString, PersonId? personId,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var module in new[] { "OrganizationAccess", "TradingRelationships", "Agreements", "PricePublication" })
            builder.UseSetting($"ConnectionStrings:{module}", connectionString);

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new[] { "OrganizationAccess", "TradingRelationships", "Agreements", "PricePublication" }
                .ToDictionary(module => $"ConnectionStrings:{module}", _ => (string?)connectionString)));

        builder.ConfigureServices(services => services.AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<TestAuthenticationOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName, options => options.PersonId = personId));
        if (configureServices is not null) builder.ConfigureServices(configureServices);
    }
}

internal sealed class TestAuthenticationOptions : AuthenticationSchemeOptions
{
    public PersonId? PersonId { get; set; }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<TestAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<TestAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgreementsTest";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Options.PersonId is not { } personId)
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim("pricesync/person_id", personId.Value.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
