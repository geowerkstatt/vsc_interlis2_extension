using Geowerkstatt.Interlis.LanguageServer.Workspace;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using FileSystemWatcher = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Handler for the client's file watcher notifications (workspace/didChangeWatchedFiles), which keep the
/// <see cref="WorkspaceModelIndex"/> in step with the INTERLIS files on disk.
/// </summary>
internal class WatchedFilesHandler(WorkspaceModelIndex workspaceModelIndex) : DidChangeWatchedFilesHandlerBase
{
    /// <inheritdoc />
    public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
    {
        foreach (var change in request.Changes)
        {
            workspaceModelIndex.OnWatchedFileChanged(change.Uri, change.Type);
        }

        return Unit.Task;
    }

    /// <inheritdoc />
    protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        Watchers = new Container<FileSystemWatcher>(new FileSystemWatcher
        {
            GlobPattern = new GlobPattern("**/*.ili"),
            Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete,
        }),
    };
}
