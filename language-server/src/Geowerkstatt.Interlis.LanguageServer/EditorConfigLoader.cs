using System.Text.RegularExpressions;

namespace Geowerkstatt.Interlis.LanguageServer;

public static class EditorConfigLoader
{
    public static Dictionary<string, string> LoadFromWorkspace()
    {
        var config = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(WorkspaceInfo.WorkspaceRoot)) return config;

        var editorConfigPath = Path.Combine(WorkspaceInfo.WorkspaceRoot, ".editorconfig");
        if (!File.Exists(editorConfigPath)) return config;
        
        foreach (var line in File.ReadAllLines(editorConfigPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
            var match = Regex.Match(trimmed, @"^([\w\.-]+)\s*=\s*(.+)$");
            if (match.Success)
            {
                config[match.Groups[1].Value] = match.Groups[2].Value;
            }
        }
        return config;
    }
}
