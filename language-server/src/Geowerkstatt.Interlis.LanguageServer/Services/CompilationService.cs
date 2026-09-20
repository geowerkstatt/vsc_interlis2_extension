using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.LanguageServer.Diagnostics;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Globalization;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

/// <summary>
/// Compiles a document together with the models it imports, supplying the imported models from the INTERLIS model
/// repositories.
/// </summary>
public sealed class CompilationService(
    RepositorySearcher repositorySearcher,
    ILoggerFactory loggerFactory,
    ExternalImportFileService externalImportFileService
) : IModelResolver
{
    private readonly ILogger<CompilationService> logger = loggerFactory.CreateLogger<CompilationService>();

    /// <summary>
    /// Compiles the INTERLIS source code of a document with its transitive imports (see
    /// <see cref="InterlisReader.ReadModelWithImportsAsync"/>). The problems the compiler reports on the way are
    /// collected instead of logged, so a compilation's diagnostics are exactly those of the returned
    /// <see cref="Compilation"/>.
    /// </summary>
    /// <param name="source">The INTERLIS source code of the document.</param>
    /// <param name="uri">The URI of the document.</param>
    /// <param name="cancellationToken">Cancels the compilation between resolving imported models.</param>
    /// <returns>The compiled environment and the problems reported for it.</returns>
    public async Task<Compilation> CompileAsync(string source, DocumentUri uri, CancellationToken cancellationToken = default)
    {
        var collector = new DiagnosticCollector();
        using var compileLoggerFactory = LoggerFactory.Create(builder => builder.AddProvider(collector));
        var reader = new InterlisReader(compileLoggerFactory);

        var environment = await reader.ReadModelWithImportsAsync(new StringReader(source), this, uri.ToString(), cancellationToken);
        return new Compilation(environment, collector.Diagnostics);
    }

    /// <summary>
    /// Supplies an imported model from the repositories: the model of the given name published for the importing
    /// model's INTERLIS version, or <see langword="null"/> if there is none or it cannot be loaded. The model's source
    /// is also stored as a local file (see <see cref="ExternalImportFileService"/>), whose URI becomes the model's
    /// source URI so that positions in it can be navigated to.
    /// </summary>
    public async ValueTask<(TextReader Reader, string? SourceUri)?> OpenModelAsync(string modelName, double? languageVersion, CancellationToken cancellationToken)
    {
        try
        {
            var schemaLanguage = languageVersion == null ? null : "ili" + languageVersion.Value.ToString(CultureInfo.InvariantCulture).Replace('.', '_');
            var foundModels = await repositorySearcher.SearchModels(m => m.Name == modelName && (schemaLanguage == null || m.SchemaLanguage == schemaLanguage));
            if (foundModels.Count == 0)
            {
                logger.LogWarning("Model '{ModelName}' for version {Version} not found in repository.", modelName, languageVersion);
            }
            else if (foundModels.Count > 1)
            {
                logger.LogWarning("Multiple models '{ModelName}' for version {Version} found in repository.", modelName, languageVersion);
            }

            var model = foundModels.FirstOrDefault();
            if (model?.FileContent?.Content is not { } content)
            {
                return null;
            }

            var localUri = await externalImportFileService.GetModelUriAsync(model);
            return (new StringReader(content), localUri?.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The import stays unresolved, which the compiler reports at the IMPORTS entry.
            logger.LogError(ex, "Failed to load imported model '{ModelName}'.", modelName);
            return null;
        }
    }
}
