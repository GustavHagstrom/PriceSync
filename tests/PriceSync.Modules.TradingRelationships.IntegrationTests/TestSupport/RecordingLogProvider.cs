using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;


public sealed class RecordingLogProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
    public ConcurrentQueue<Dictionary<string, object?>> Entries { get; } = new();
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);
    public void Dispose() { }

    private sealed class RecordingLogger(RecordingLogProvider provider, string categoryName) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider._scopes.Push(state);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!categoryName.EndsWith("Handler", StringComparison.Ordinal) &&
                !categoryName.EndsWith("Endpoint", StringComparison.Ordinal)) return;
            var fields = new Dictionary<string, object?>();
            provider._scopes.ForEachScope((scope, values) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> properties)
                    foreach (var property in properties) values[property.Key] = property.Value;
            }, fields);
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
                foreach (var property in properties) fields[property.Key] = property.Value;
            provider.Entries.Enqueue(fields);
        }
    }
}

  

