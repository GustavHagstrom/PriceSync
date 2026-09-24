using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Features.ListMyOrganizations;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.IntegrationTests;

public sealed class ApiProblemDetailsTests
{
    [Fact]
    public async Task Organization_endpoints_fail_closed_when_production_authentication_is_unavailable()
    {
        await using var application = new DefaultApiApplication();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/v1/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("code").GetString().Should().Be("authentication_required");
    }

    [Fact]
    public async Task Unexpected_failures_return_sanitized_problem_details()
    {
        await using var application = new OrganizationAccessApiApplication(
            "Host=unused",
            new PersonId(Guid.NewGuid()),
            services => services.AddScoped<ListMyOrganizationsHandler>(_ =>
                throw new InvalidOperationException("sensitive failure detail")));
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/v1/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("sensitive failure detail");

        using var problem = JsonDocument.Parse(body);
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(500);
        problem.RootElement.GetProperty("code").GetString().Should().Be("internal_server_error");
    }

    private sealed class DefaultApiApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:OrganizationAccess", "Host=unused");
            builder.UseSetting("ConnectionStrings:TradingRelationships", "Host=unused");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrganizationAccess"] = "Host=unused",
                    ["ConnectionStrings:TradingRelationships"] = "Host=unused",
                });
            });
        }
    }

}
