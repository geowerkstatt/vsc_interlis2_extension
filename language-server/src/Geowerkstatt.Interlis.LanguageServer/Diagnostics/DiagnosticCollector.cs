using Geowerkstatt.Interlis.Compiler.AST;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Diagnostics;

/// <summary>
/// Collects the problems the compiler logs during one compilation. The compiler reports every problem as a log entry
/// whose message template has a <c>{Range}</c> placeholder holding the <see cref="RangePosition"/> of the offending
/// construct; this provider reads that value off the entry state. The range alone makes an entry a problem, whatever
/// its level; log entries without a range are not diagnostics and are ignored.
/// <para>
/// Intended for one compilation each: create it, compile with a logger factory that has it as its only provider,
/// then read <see cref="Diagnostics"/>.
/// </para>
/// </summary>
internal sealed class DiagnosticCollector : ILoggerProvider
{
    /// <summary>The message-template placeholder the compiler puts the <see cref="RangePosition"/> of a problem in.</summary>
    private const string RangePlaceholder = "Range";

    private readonly ConcurrentQueue<CompilerDiagnostic> diagnostics = new();

    /// <summary>The problems collected so far, in the order they were reported.</summary>
    public IReadOnlyList<CompilerDiagnostic> Diagnostics => diagnostics.ToArray();

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CollectingLogger(this);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CollectingLogger(DiagnosticCollector collector) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object?>> values
                && values.FirstOrDefault(pair => pair.Key == RangePlaceholder).Value is RangePosition range)
            {
                collector.diagnostics.Enqueue(new CompilerDiagnostic(logLevel, formatter(state, exception), range));
            }
        }
    }
}
