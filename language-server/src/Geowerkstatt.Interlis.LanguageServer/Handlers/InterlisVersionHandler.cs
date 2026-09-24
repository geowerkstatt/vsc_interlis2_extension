using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Command handler reporting the INTERLIS version a document declares in its header, which the client shows in the
/// status bar. Responds to workspace.executeCommand requests using the executeCommandProvider capability of the
/// language server protocol.
/// </summary>
public class InterlisVersionHandler : ExecuteTypedResponseCommandHandlerBase<InterlisVersionOptions, double?>
{
    public const string Command = "getInterlisVersion";

    private readonly ILogger<InterlisVersionHandler> logger;
    private readonly WorkspaceModelIndex workspaceModelIndex;
    private readonly InterlisEnvironmentCache environmentCache;

    public InterlisVersionHandler(ILogger<InterlisVersionHandler> logger, WorkspaceModelIndex workspaceModelIndex, InterlisEnvironmentCache environmentCache, ISerializer serializer)
        : base(Command, serializer)
    {
        this.logger = logger;
        this.workspaceModelIndex = workspaceModelIndex;
        this.environmentCache = environmentCache;
    }

    /// <summary>
    /// Handles the getInterlisVersion requests.
    /// </summary>
    /// <param name="options">The requested options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    /// <returns>The INTERLIS version of the document, or <c>null</c> if it has no valid header or is not known.</returns>
    public override async Task<double?> Handle(InterlisVersionOptions options, CancellationToken cancellationToken)
    {
        if (options?.Uri is not { } uriString)
        {
            return null;
        }

        // The index re-parses a document on every change, without compiling it, so it answers while the user types.
        var uri = DocumentUri.From(uriString);
        if (workspaceModelIndex.Find(uri) is { } file)
        {
            return file.Version;
        }

        try
        {
            // Repository models the editor shows are not indexed; they never change, so they compile once.
            var compilation = await environmentCache.GetCompilationAsync(uri, cancellationToken);
            return compilation.Environment.Version;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to determine the INTERLIS version of {Uri}", uriString.Replace("\r", string.Empty).Replace("\n", string.Empty));
            return null;
        }
    }
}
