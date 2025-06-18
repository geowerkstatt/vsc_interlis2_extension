using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.Compiler.CreateAST;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

public sealed class InterlisResolveService(InterlisReader interlisReader, RepositorySearcher repositorySearcher, ILoggerFactory loggerFactory)
{
    private readonly ILogger<InterlisResolveService> logger = loggerFactory.CreateLogger<InterlisResolveService>();

    public async Task<InterlisEnvironment> ResolveAsync(TextReader textReader, string? sourceUri = null)
    {
        var interlisEnvironment = interlisReader.ReadFile(textReader, sourceUri);

        await ResolveImportsAsync(interlisEnvironment);

        var referenceResolver = new Interlis24AstReferenceResolverVisitor(loggerFactory);
        interlisEnvironment.Accept(referenceResolver);

        return interlisEnvironment;
    }

    private async Task ResolveImportsAsync(InterlisEnvironment interlisEnvironment)
    {
        var processedImports = new HashSet<string>();
        var importVisitor = new Interlis24ImportVisitor();
        var imports = interlisEnvironment.Accept(importVisitor) ?? new HashSet<string>();

        do
        {
            foreach (var import in imports)
            {
                if (!processedImports.Add(import))
                {
                    continue;
                }

                var schemaLanguage = "ili" + interlisEnvironment.Version?.ToString().Replace('.', '_');
                var importedModel = (await repositorySearcher.SearchModels(m => m.Name == import && m.SchemaLanguage == schemaLanguage)).FirstOrDefault();
                if (importedModel?.FileContent != null)
                {
                    try
                    {
                        var importedEnvironment = interlisReader.ReadFile(new StringReader(importedModel.FileContent.Content), importedModel.Uri?.ToString());
                        if (importedEnvironment.Version == interlisEnvironment.Version)
                        {
                            AddEnvironmentContent(interlisEnvironment, importedEnvironment);
                        }
                        else
                        {
                            logger.LogError("Imported model '{ImportedModel}' has version {ImportedVersion}, expected version {Version}.", importedModel.Name, importedEnvironment.Version, interlisEnvironment.Version);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to compile imported model '{ImportedModel}' from {Uri}.", importedModel.Name, importedModel.Uri);
                    }
                }
            }

            imports = interlisEnvironment.Accept(importVisitor) ?? new HashSet<string>();
        } while (processedImports.Count != imports.Count);
    }

    private static void AddEnvironmentContent(InterlisEnvironment baseEnvironment, InterlisEnvironment importedEnvironment)
    {
        foreach (var (modelName, model) in importedEnvironment.Content)
        {
            baseEnvironment.Content.TryAdd(modelName, model);
        }
    }
}
