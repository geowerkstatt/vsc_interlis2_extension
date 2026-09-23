using Geowerkstatt.Interlis.LanguageServer;
using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Diagnostics;
using Geowerkstatt.Interlis.LanguageServer.Handlers;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Geowerkstatt.Interlis.LanguageServer.Workspace;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
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

            services.AddSingleton<OpenDocuments>();
            services.AddSingleton<InterlisEnvironmentCache>();
            services.AddSingleton<UiLanguageContext>();
            services.AddSingleton<DiagnosticsPublisher>();
            services.AddSingleton<WorkspaceModelIndex>();

            services.AddSingleton<ExternalImportFileService>();
            services.AddTransient<CompilationService>();
            services.AddTransient<SymbolLookup>();
            services.AddSingleton(provider => new RepositorySearcher(
                provider.GetRequiredService<IRepositoryCrawler>(),
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<ILoggerFactory>()
            ));
            services.AddSingleton<IRepositoryCrawler, RepositoryCrawler>();
            services.AddHttpClient();

            services.AddTransient<ReferenceCollectorVisitor>();

            services.AddSingleton(provider =>
            {
                var serverOptions = provider.GetRequiredService<IOptions<ServerOptions>>().Value;
                return TextDocumentSelector.ForLanguage(serverOptions.LanguageName);
            });
        })
        .WithConfigurationSection(DocumentationOptions.ConfigSection)
        .WithHandler<TextDocumentSyncHandler>()
        .WithHandler<GenerateMarkdownHandler>()
        .WithHandler<FormatterHandler>()
        .WithHandler<DefinitionHandler>()
        .WithHandler<GenerateDiagramHandler>()
        .WithHandler<WatchedFilesHandler>()
        .WithHandler<DocumentSymbolHandler>()
        .WithHandler<ReferencesHandler>()
        .WithHandler<RenameHandler>()
        .WithHandler<HoverHandler>()
        .WithHandler<InterlisVersionHandler>()
        .OnInitialize((server, request, _) =>
        {
            // The publisher (and the caches it depends on) work from events, so they must exist before anything
            // raises one: before the workspace scan below and before the first document is opened.
            server.Services.GetRequiredService<DiagnosticsPublisher>();

            // Index the INTERLIS files of the workspace so imports resolve to them before the model repositories.
            var folders = request.WorkspaceFolders?.Select(folder => folder.Uri).ToList() ?? [];
            if (folders.Count == 0 && request.RootUri is { } rootUri)
            {
                folders.Add(rootUri);
            }

            server.Services.GetRequiredService<WorkspaceModelIndex>().ScanWorkspace(folders);

            // Client reports its UI language (e.g. "de", "fr") so that output
            // defaults match the user's environment when the workspace setting
            // is "auto". Unknown values fall through to the German bundle.
            var uiLang = (request.InitializationOptions as JToken)?["uiLanguage"]?.ToString();
            if (!string.IsNullOrEmpty(uiLang))
            {
                server.Services.GetRequiredService<UiLanguageContext>().Language = uiLang;
            }
            return Task.CompletedTask;
        });
}).ConfigureAwait(false);

server.LogInfo("Starting INTERLIS language server...");

await server.WaitForExit.ConfigureAwait(false);
