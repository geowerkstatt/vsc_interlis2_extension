using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Geowerkstatt.Interlis.RepositoryCrawler.Models;
using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

/// <summary>
/// Service to handle imported INTERLIS files.
/// </summary>
public sealed class ExternalImportFileService(ILogger<ExternalImportFileService> logger, RepositorySearcher repositorySearcher) : IDisposable
{
    private static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), ServerConstants.TempFolder);

    /// <summary>
    /// Gets the URI of the INTERLIS file containing the given <see cref="ModelDef"/>.
    /// </summary>
    /// <remarks>
    /// External files are stored in a temporary directory and the URI points to the local file system.
    /// </remarks>
    /// <param name="modelDef">The <see cref="ModelDef"/> to resolve.</param>
    /// <returns>The URI of the INTERLIS file containing the <paramref name="modelDef"/>.</returns>
    public async Task<Uri?> GetModelUriAsync(ModelDef modelDef)
    {
        var modelName = modelDef.Name;

        try
        {
            var modelDeclaration = await repositorySearcher.SearchModel(modelName);
            if (modelDeclaration is null)
                return null;

            var fileCacheLocation = GetTempLocalFilePath(modelDeclaration);
            if (File.Exists(fileCacheLocation))
                return new Uri(fileCacheLocation);

            Directory.CreateDirectory(Path.GetDirectoryName(fileCacheLocation) ?? TempDirectory);
            File.WriteAllText(fileCacheLocation, modelDeclaration.FileContent?.Content);

            return new Uri(fileCacheLocation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load model \"{ModelName}\"", modelName);
            return null;
        }
    }

    private static string GetTempLocalFilePath(Model modelDeclaration)
    {
        var host = modelDeclaration.ModelRepository?.Uri.Host ?? string.Empty;
        var paths = modelDeclaration.ModelRepository?.Uri.AbsolutePath.Split('/') ?? Array.Empty<string>();
        var filePaths = modelDeclaration.File.Split('/') ?? Array.Empty<string>();

        return Path.Combine([TempDirectory, host, .. paths, .. filePaths]);
    }

    public void Dispose()
    {
        Directory.Delete(TempDirectory, true);
    }
}
