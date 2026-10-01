using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

internal sealed class ApiApplication(
    string connectionString,
    PersonId? personId,
    IOrganizationAuthorizer organizationAuthorizer,
    IOrganizationStatusReader organizationStatusReader,
    TimeProvider timeProvider,
    IReadOnlyCollection<IInterceptor>? interceptors = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:OrganizationAccess", "Host=unused");
        builder.UseSetting("ConnectionStrings:TradingRelationships", connectionString);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrganizationAccess"] = "Host=unused",
                ["ConnectionStrings:TradingRelationships"] = connectionString,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TradingRelationshipsDbContext>>();
            services.AddDbContext<TradingRelationshipsDbContext>(options =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                        TradingRelationshipsDbContext.MigrationsHistoryTableName,
                        TradingRelationshipsDbContext.SchemaName));

                if (interceptors is not null)
                {
                    options.AddInterceptors(interceptors);
                }
            });

            services.RemoveAll<IOrganizationAuthorizer>();
            services.AddScoped(_ => organizationAuthorizer);
            services.RemoveAll<IOrganizationStatusReader>();
            services.AddScoped(_ => organizationStatusReader);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(timeProvider);

            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<TestAuthenticationOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    options => options.PersonId = personId);
        });
    }
}

internal sealed class TestAuthenticationOptions : AuthenticationSchemeOptions
{
    public PersonId? PersonId { get; set; }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<TestAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<TestAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "TradingRelationshipsTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Options.PersonId is not { } personId)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim("pricesync/person_id", personId.Value.ToString())],
            SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
