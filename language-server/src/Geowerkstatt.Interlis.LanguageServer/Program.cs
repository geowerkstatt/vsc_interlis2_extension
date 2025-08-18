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
using Microsoft.Extensions.Options;
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
            options.AddJsonFile("appsettings.json");
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
            services
                .AddOptions<ServerOptions>()
                .BindConfiguration(ServerOptions.ConfigSection);

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

            services.AddSingleton(provider =>
            {
                var serverOptions = provider.GetRequiredService<IOptions<ServerOptions>>().Value;
                return TextDocumentSelector.ForLanguage(serverOptions.LanguageName);
            });
        })
        .WithHandler<TextDocumentSyncHandler>()
        .WithHandler<GenerateMarkdownHandler>()
        .WithHandler<FormatterHandler>()
        .WithHandler<DefinitionHandler>()
        .WithHandler<GenerateDiagramHandler>();
}).ConfigureAwait(false);

server.LogInfo("Starting INTERLIS language server...");

await server.WaitForExit.ConfigureAwait(false);
