using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.Compiler.CreateAST;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Geowerkstatt.Interlis.RepositoryCrawler.Models;
using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

/// <summary>
/// Service to resolve imported INTERLIS models.
/// </summary>
public sealed class ImportResolveService(
    InterlisReader interlisReader,
    RepositorySearcher repositorySearcher,
    ILoggerFactory loggerFactory,
    ModelImportVisitor modelImportVisitor,
    ExternalImportFileService externalImportFileService
)
{
    private readonly ILogger<ImportResolveService> logger = loggerFactory.CreateLogger<ImportResolveService>();

    /// <summary>
    /// Compiles the INTERLIS source code into an <see cref="InterlisEnvironment"/> without resolving references.
    /// </summary>
    /// <param name="source">The INTERLIS source code.</param>
    /// <param name="uri">The URI of the INTERLIS file.</param>
    /// <returns>The <see cref="InterlisEnvironment"/> containing the compiled file.</returns>
    public InterlisEnvironment CompileWithoutReferenceResolve(TextReader source, string? uri)
    {
        var environment = interlisReader.ReadRule(source, (parser, visitor) => visitor.VisitInterlis(parser.interlis()));
        foreach (var model in environment.Content.Values)
        {
            if (model != InternalModel.Interlis)
            {
                model.SourceUri = uri;
            }
        }

        return environment;
    }

    /// <summary>
    /// Resolves the imports and adds the models to the INTERLIS environment.
    /// </summary>
    /// <param name="environment">The environment to resolve.</param>
    public async Task ResolveImportsAsync(InterlisEnvironment environment)
    {
        await ResolveImportedModelsAsync(environment);

        // Resolve references after adding the imported models to the environment
        var referenceResolver = new Interlis24AstReferenceResolverVisitor(loggerFactory);
        environment.Accept(referenceResolver);
    }

    private async Task ResolveImportedModelsAsync(InterlisEnvironment environment)
    {
        var processedImports = new HashSet<string>();
        var foundImports = environment.Accept(modelImportVisitor) ?? new HashSet<string>();

        while (processedImports.Count < foundImports.Count)
        {
            foreach (var import in foundImports)
            {
                if (!processedImports.Add(import))
                {
                    continue;
                }

                var schemaLanguage = environment.Version == null ? null : "ili" + environment.Version?.ToString().Replace('.', '_');
                var foundModels = await repositorySearcher.SearchModels(m => m.Name == import && (schemaLanguage == null || m.SchemaLanguage == schemaLanguage));
                if (foundModels.Count == 0)
                {
                    logger.LogWarning("Model '{ModelName}' for version {Version} not found in repository.", import, environment.Version);
                }
                else if (foundModels.Count > 1)
                {
                    logger.LogWarning("Multiple models '{ModelName}' for version {Version} found in repository.", import, environment.Version);
                }

                var importedModel = foundModels.FirstOrDefault();
                if (importedModel?.FileContent != null)
                {
                    await CompileIntoEnvironmentAsync(environment, importedModel);
                }
            }

            foundImports = environment.Accept(modelImportVisitor) ?? new HashSet<string>();
        }
    }

    private async Task CompileIntoEnvironmentAsync(InterlisEnvironment environment, Model importedModel)
    {
        try
        {
            var localUri = await externalImportFileService.GetModelUriAsync(importedModel);
            var importedEnvironment = CompileWithoutReferenceResolve(new StringReader(importedModel.FileContent?.Content ?? ""), localUri?.ToString());
            if (importedEnvironment.Version == environment.Version)
            {
                AddEnvironmentContent(environment, importedEnvironment);
            }
            else
            {
                logger.LogError("Imported model '{ImportedModel}' has version {ImportedVersion}, expected version {Version}.", importedModel.Name, importedEnvironment.Version, environment.Version);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compile imported model '{ImportedModel}' from {Uri}.", importedModel.Name, importedModel.Uri);
        }
    }

    private static void AddEnvironmentContent(InterlisEnvironment baseEnvironment, InterlisEnvironment importedEnvironment)
    {
        foreach (var (modelName, model) in importedEnvironment.Content)
        {
            baseEnvironment.Content.TryAdd(modelName, model);
        }
    }
}
