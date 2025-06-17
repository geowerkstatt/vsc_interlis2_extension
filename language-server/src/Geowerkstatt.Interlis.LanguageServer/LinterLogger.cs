using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer;

public class LinterLogger : ILogger
{
    public List<string> Messages { get; } = new List<string>();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        Messages.Add(formatter(state, exception));
    }
}