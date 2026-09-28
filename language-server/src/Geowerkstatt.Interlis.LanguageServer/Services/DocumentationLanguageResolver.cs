using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

/// <summary>
/// Resolves the <see cref="DocumentationLocalization"/> to use for a request: an explicit
/// request language wins, otherwise the <c>interlis.documentation.language</c> workspace
/// setting is fetched from the client, falling back to the client's UI language.
/// </summary>
public class DocumentationLanguageResolver
{
    private readonly ILogger<DocumentationLanguageResolver> logger;
    private readonly ILanguageServerFacade languageServer;
    private readonly UiLanguageContext uiLanguageContext;

    public DocumentationLanguageResolver(ILogger<DocumentationLanguageResolver> logger, ILanguageServerFacade languageServer, UiLanguageContext uiLanguageContext)
    {
        this.logger = logger;
        this.languageServer = languageServer;
        this.uiLanguageContext = uiLanguageContext;
    }

    public async Task<DocumentationLocalization> ResolveAsync(string? requestLanguage, CancellationToken cancellationToken)
    {
        // Webview dropdown overrides the workspace setting; skip the round-trip when present.
        if (!string.IsNullOrEmpty(requestLanguage))
        {
            return DocumentationLocalization.For(uiLanguageContext.Resolve(requestLanguage));
        }

        try
        {
            var configRequest = new ConfigurationParams
            {
                Items = new[]
                {
                    new ConfigurationItem
                    {
                        Section = DocumentationOptions.ConfigSection
                    }
                }
            };

            var response = await languageServer.Workspace.RequestConfiguration(configRequest, cancellationToken);
            if (response.Any())
            {
                var configToken = response.First();
                var language = uiLanguageContext.Resolve(configToken?["language"]?.ToString());
                return DocumentationLocalization.For(language);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve configuration, using defaults");
        }

        return DocumentationLocalization.For(uiLanguageContext.Language);
    }
}
