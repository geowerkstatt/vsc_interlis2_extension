using Geowerkstatt.Interlis.LanguageServer;
using Geowerkstatt.Interlis.LanguageServer.Handlers;
using Geowerkstatt.Interlis.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;

var server = await LanguageServer.From(options =>
{
    options
        .WithInput(Console.OpenStandardInput())
        .WithOutput(Console.OpenStandardOutput())
        .ConfigureLogging(options => {
            options
                .ClearProviders()
                .AddLanguageProtocolLogging()
                .SetMinimumLevel(LogLevel.Information);
        })
        .WithServices(services =>
        {
            services.AddSingleton<FileContentCache>();
            services.AddTransient<InterlisReader>();
        })
        .WithHandler<TextDocumentSyncHandler>()
        .WithHandler<GenerateMarkdownHandler>();
}).ConfigureAwait(false);

server.LogInfo("Starting INTERLIS language server...");

await server.WaitForExit.ConfigureAwait(false);
