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
/// The compiler's warning about an INTERLIS version other than 2.4 is dropped as well: most models in use are still
/// written in 2.3, and the client shows the version with its support note in the status bar instead. The warning is
/// recognized by its message template, so it shows again should the compiler reword it. Remove this workaround once
/// the compiler supports INTERLIS 2.3.
/// </para>
/// <para>
/// Intended for one compilation each: create it, compile with a logger factory that has it as its only provider,
/// then read <see cref="Diagnostics"/>.
/// </para>
/// </summary>
internal sealed class DiagnosticCollector : ILoggerProvider
{
    /// <summary>The message-template placeholder the compiler puts the <see cref="RangePosition"/> of a problem in.</summary>
    private const string RangePlaceholder = "Range";

    /// <summary>The key under which a log entry's state holds its message template.</summary>
    private const string OriginalFormatKey = "{OriginalFormat}";

    /// <summary>The message template of the compiler's warning about an INTERLIS version other than 2.4.</summary>
    private const string UnsupportedVersionTemplate = "Unsupported INTERLIS version {Version} at {Range}. Only version 2.4 is supported.";

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
                && values.FirstOrDefault(pair => pair.Key == RangePlaceholder).Value is RangePosition range
                && values.FirstOrDefault(pair => pair.Key == OriginalFormatKey).Value as string != UnsupportedVersionTemplate)
            {
                collector.diagnostics.Enqueue(new CompilerDiagnostic(logLevel, formatter(state, exception), range));
            }
        }
    }
}
