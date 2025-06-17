using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Geowerkstatt.Interlis.LanguageServer.Cache
{
    internal interface ICache<T> where T : class
    {
        public event Action<DocumentUri>? DocumentInvalidated;

        public T Get(DocumentUri uri);

    }
}
