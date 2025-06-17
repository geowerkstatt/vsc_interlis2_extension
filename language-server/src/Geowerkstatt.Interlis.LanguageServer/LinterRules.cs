namespace Geowerkstatt.Interlis.LanguageServer
{
    public class LinterRule
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public bool DefaultEnabled { get; set; }
        public Func<LinterRuleContext, bool> IsEnabled { get; set; } = _ => true;
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
                Description = "Use INTERLIS.Boolean instead of Boolean",
                DefaultEnabled = true
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
}
