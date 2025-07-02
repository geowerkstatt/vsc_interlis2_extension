using Geowerkstatt.Interlis.RepositoryCrawler.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

/// <summary>
/// Service to handle imported INTERLIS files.
/// </summary>
public sealed class ExternalImportFileService(ILogger<ExternalImportFileService> logger, IOptions<ServerOptions> serverOptions) : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), serverOptions.Value.TempFolderName);

    /// <summary>
    /// Gets the URI of the INTERLIS file containing the given <see cref="Model"/>.
    /// </summary>
    /// <remarks>
    /// External files are stored in a temporary directory and the URI points to the local file system.
    /// </remarks>
    /// <param name="model">The <see cref="Model"/> to resolve.</param>
    /// <returns>The URI of the INTERLIS file containing the <paramref name="model"/>.</returns>
    public async Task<Uri?> GetModelUriAsync(Model model)
    {
        try
        {
            var fileCacheLocation = GetTempLocalFilePath(model);
            if (File.Exists(fileCacheLocation))
                return new Uri(fileCacheLocation);

            Directory.CreateDirectory(Path.GetDirectoryName(fileCacheLocation) ?? tempDirectory);
            await File.WriteAllTextAsync(fileCacheLocation, model.FileContent?.Content);
            File.SetAttributes(fileCacheLocation, FileAttributes.ReadOnly);

            return new Uri(fileCacheLocation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load model \"{ModelName}\"", model.Name);
            return null;
        }
    }

    private string GetTempLocalFilePath(Model modelDeclaration)
    {
        var host = modelDeclaration.ModelRepository?.Uri.Host ?? string.Empty;
        var paths = modelDeclaration.ModelRepository?.Uri.AbsolutePath.Split('/') ?? Array.Empty<string>();
        var filePaths = modelDeclaration.File.Split('/') ?? Array.Empty<string>();

        return Path.Combine([tempDirectory, host, .. paths, .. filePaths]);
    }

    public void Dispose()
    {
        Directory.Delete(tempDirectory, true);
    }
}
