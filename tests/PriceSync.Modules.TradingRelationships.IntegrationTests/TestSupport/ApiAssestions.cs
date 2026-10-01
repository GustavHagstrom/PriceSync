using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;

internal static class ApiAssestions
{
    public static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be(expectedCode);
    }
}
