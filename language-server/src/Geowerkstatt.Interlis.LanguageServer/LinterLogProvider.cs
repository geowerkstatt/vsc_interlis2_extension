using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer;

public class LinterLogProvider : ILoggerProvider
{
    private readonly LinterLogger logger = new LinterLogger();

    public ILogger CreateLogger(string categoryName) => logger;
    
    public void Dispose()
    {
    }

    public List<string> GetMessages() => logger.Messages;
}