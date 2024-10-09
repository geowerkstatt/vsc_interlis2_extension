using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer;

public sealed class FileContentCache
{
    private readonly ConcurrentDictionary<string, string> buffers = new ConcurrentDictionary<string, string>();

    public void UpdateBuffer(DocumentUri uri, string buffer)
    {
        buffers.AddOrUpdate(uri.ToString(), buffer, (k, v) => buffer);
    }

    public string GetBuffer(DocumentUri uri)
    {
        return buffers.TryGetValue(uri.ToString(), out var buffer) ? buffer : string.Empty;
    }

    public void ClearBuffer(DocumentUri uri)
    {
        buffers.TryRemove(uri.ToString(), out _);
    }
}
