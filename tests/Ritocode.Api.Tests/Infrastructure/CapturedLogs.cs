using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// Every log line the host writes, with the scopes that were open — what the JSON console formatter
/// prints as <c>Scopes</c> — so a test can find a request's lines as an operator would.
/// </summary>
public sealed class CapturedLogs : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<LogLine> _lines = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyCollection<LogLine> Lines => [.. _lines];

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class Logger(CapturedLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => owner._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scope = new Dictionary<string, object?>(StringComparer.Ordinal);

            owner._scopes.ForEachScope(
                (value, into) =>
                {
                    if (value is IEnumerable<KeyValuePair<string, object>> pairs)
                    {
                        foreach (var (key, item) in pairs)
                        {
                            into[key] = item;
                        }
                    }
                },
                scope);

            var fields = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : [];

            owner._lines.Enqueue(new LogLine(category, logLevel, formatter(state, exception), scope, fields));
        }
    }
}

/// <param name="Scope">The key-value scopes open when the line was written, such as <c>RequestId</c>.</param>
/// <param name="Fields">The line's own structured values.</param>
public sealed record LogLine(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Scope,
    IReadOnlyDictionary<string, object?> Fields);
