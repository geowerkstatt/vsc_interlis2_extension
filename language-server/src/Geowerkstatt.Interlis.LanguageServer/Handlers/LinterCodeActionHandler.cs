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
            var filePath = request.TextDocument.Uri.GetFileSystemPath();
            var editorConfig = EditorConfigLoader.LoadFromWorkspace();
            var ruleContext = new LinterRuleContext { EditorConfig = editorConfig };

            foreach (var diagnostic in request.Context.Diagnostics)
            {
                if (diagnostic == null || diagnostic.Code.HasValue == false) continue;
                
                var rule = LinterRules.All.Find(r => diagnostic.Code == r.Id);
                if (rule == null || LinterRules.IsRuleEnabled(rule.Id, ruleContext) == false || rule.GetWorkspaceEdit == null) continue;

                var edit = rule.GetWorkspaceEdit(request, request.Context.Diagnostics, ruleContext);
                if (edit == null) continue;

                actions.Add(new CodeAction
                {
                    Title = rule.Description,
                    Kind = CodeActionKind.QuickFix,
                    Diagnostics = new[] { diagnostic },
                    Edit = edit
                });
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
