using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.LanguageServer;
using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;

var server = await LanguageServer.From(options =>
{
    options
        .WithInput(Console.OpenStandardInput())
        .WithOutput(Console.OpenStandardOutput())
        .ConfigureLogging(options =>
        {
            options
                .ClearProviders()
                .AddLanguageProtocolLogging()
                .SetMinimumLevel(LogLevel.Information);
        })
        .WithServices(services =>
        {
            services.AddSingleton<FileContentCache>();
            services.AddSingleton<InterlisEnvironmentCache>();
            services.AddSingleton<ReferenceCache>();
            services.AddTransient<InterlisReader>();
            services.AddSingleton(TextDocumentSelector.ForLanguage(ServerConstants.InterlisLanguageName));
        })
        .WithHandler<TextDocumentSyncHandler>()
        .WithHandler<GenerateMarkdownHandler>()
        .WithHandler<DefinitionHandler>()
        .WithHandler<GenerateDiagramHandler>();
}).ConfigureAwait(false);

server.LogInfo("Starting INTERLIS language server...");

await server.WaitForExit.ConfigureAwait(false);
