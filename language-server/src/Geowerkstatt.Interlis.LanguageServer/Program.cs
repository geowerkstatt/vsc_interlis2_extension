using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.LanguageServer;
using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Handlers;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Microsoft.Extensions.Configuration;
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
        .ConfigureConfiguration(options =>
        {
            options.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "RepositoryCrawler:RootRepositoryUri", ServerConstants.DefaultRootRepositoryUri },
            });
        })
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

            services.AddSingleton<ExternalImportFileService>();
            services.AddTransient<ImportResolveService>();
            services.AddSingleton(provider => new RepositorySearcher(
                provider.GetRequiredService<IRepositoryCrawler>(),
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<ILoggerFactory>()
            ));
            services.AddSingleton<IRepositoryCrawler, RepositoryCrawler>();
            services.AddTransient<InterlisReader>();
            services.AddHttpClient();

            services.AddTransient<ReferenceCollectorVisitor>();
            services.AddTransient<ModelImportVisitor>();

            services.AddSingleton(TextDocumentSelector.ForLanguage(ServerConstants.InterlisLanguageName));
        })
        .WithHandler<TextDocumentSyncHandler>()
        .WithHandler<GenerateMarkdownHandler>()
        .WithHandler<DefinitionHandler>()
        .WithHandler<GenerateDiagramHandler>();
}).ConfigureAwait(false);

server.LogInfo("Starting INTERLIS language server...");

await server.WaitForExit.ConfigureAwait(false);
