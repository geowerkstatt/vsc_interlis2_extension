using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers
{
    internal class LinterCodeActionHandler : CodeActionHandlerBase
    {
        public override Task<CommandOrCodeActionContainer?> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            var actions = new List<CommandOrCodeAction>();

            foreach (var diagnostic in request.Context.Diagnostics)
            {
                // Only offer a fix for diagnostics about 'Boolean' (customize as needed)
                if (diagnostic.Message != null && diagnostic.Message.Contains("Boolean should be INTERLIS.BOOLEAN"))
                {
                    var edit = new WorkspaceEdit
                    {
                        Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
                        {
                            [request.TextDocument.Uri] = new[]
                            {
                                new TextEdit
                                {
                                    NewText = "INTERLIS.BOOLEAN",
                                    Range = diagnostic.Range
                                }
                            }
                        }
                    };

                    actions.Add(new CodeAction
                    {
                        Title = "Replace with INTERLIS.BOOLEAN",
                        Kind = CodeActionKind.QuickFix,
                        Diagnostics = new[] { diagnostic },
                        Edit = edit
                    });
                }
            }

            return Task.FromResult<CommandOrCodeActionContainer?>(new CommandOrCodeActionContainer(actions));
        }

        public override Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken)
        {
            // Not used for simple quick fixes
            return Task.FromResult(request);
        }

        protected override CodeActionRegistrationOptions CreateRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CodeActionRegistrationOptions
            {
                DocumentSelector = TextDocumentSelector.ForLanguage("INTERLIS2"),
                ResolveProvider = false,
                CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix)
            };
        }
    }
}
