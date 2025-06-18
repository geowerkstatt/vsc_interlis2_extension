using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Geowerkstatt.Interlis.RepositoryCrawler.Models;
using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer.Services
{
    public class ExternalImportFileService(ILogger<ExternalImportFileService> logger, RepositorySearcher repositorySearcher) : IDisposable
    {
        private static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), ServerConstants.TempFolder);

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

                File.SetAttributes(fileCacheLocation, FileAttributes.ReadOnly)

                return new Uri(fileCacheLocation);
            }
            catch (SystemException ex) {
                logger.LogError(ex.Message);
            }

            return null;
        }

        private string GetTempLocalFilePath(Model modelDeclaration)
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
}
