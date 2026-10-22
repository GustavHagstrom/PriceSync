using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace PriceSync.Modules.TradingRelationships.Infrastructure;

internal static class LogScope
{
    public static IDisposable? BeginScope(ILogger logger, string operation, Guid? tradingRelationshipId = null, Guid? personId = null, Guid? organizationId = null)
    {
        var scopeData = new Dictionary<string, object?>
        {
            ["Operation"] = operation,
            ["TradingRelationshipId"] = tradingRelationshipId,
            ["PersonId"] = personId,
            ["OrganizationId"] = organizationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString(),
        };
        return logger.BeginScope(scopeData);
    }
}
