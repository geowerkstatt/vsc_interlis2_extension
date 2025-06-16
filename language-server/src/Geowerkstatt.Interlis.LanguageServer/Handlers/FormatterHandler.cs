using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

public class FormatterHandler : DocumentFormattingHandlerBase
{
    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(DocumentFormattingCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DocumentFormattingRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.ili")
        };
    }

    public override Task<TextEditContainer> Handle(DocumentFormattingParams request, CancellationToken cancellationToken)
    {
        var edit = new TextEdit
        {
            Range = new Range(new Position(0, 0), new Position(0, 0)),
            NewText = "// Formatted by FormatterHandler\n"
        };

        return Task.FromResult(new TextEditContainer(edit));
    }
}
