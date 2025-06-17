using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Geowerkstatt.Interlis.LanguageServer;

public class LinterRule
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool DefaultEnabled { get; set; }
    public Func<CodeActionParams, IEnumerable<Diagnostic>, LinterRuleContext, WorkspaceEdit?>? GetWorkspaceEdit { get; set; }
}

public class LinterRuleContext
{
    public IDictionary<string, string> EditorConfig { get; set; } = new Dictionary<string, string>();
}

public static class LinterRules
{
    public static readonly List<LinterRule> All = new List<LinterRule>
    {
        new() {
            Id = "interlis.boolean-type",
            Description = "Use INTERLIS.BOOLEAN instead of BOOLEAN",
            DefaultEnabled = true,
            GetWorkspaceEdit = (request, diagnostics, context) =>
            {
                var edits = new List<TextEdit>();
                foreach (var diagnostic in diagnostics)
                {
                    edits.Add(new TextEdit
                    {
                        NewText = "INTERLIS.BOOLEAN",
                        Range = diagnostic.Range
                    });
                }
                if (edits.Count == 0) return null;
                return new WorkspaceEdit
                {
                    Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
                    {
                        [request.TextDocument.Uri] = edits
                    }
                };
            }
        },
    };

    public static bool IsRuleEnabled(string ruleId, LinterRuleContext context)
    {
        // Check .editorconfig (context.EditorConfig) for rule setting, fallback to default
        if (context.EditorConfig.TryGetValue($"dotnet_diagnostic.{ruleId}.enabled", out var value))
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        var rule = All.Find(r => r.Id == ruleId);
        return rule?.DefaultEnabled ?? false;
    }
}

