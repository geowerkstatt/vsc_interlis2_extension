using System.Text.RegularExpressions;

namespace Geowerkstatt.Interlis.LanguageServer;

public static class EditorConfigLoader
{
    public static Dictionary<string, string> Load(string filePath)
    {
        var config = new Dictionary<string, string>();
        // Normalize file path for Windows/Unix compatibility
        var normalizedPath = filePath;
        if (normalizedPath.StartsWith("/") && normalizedPath.Length > 2 && normalizedPath[2] == ':')
        {
            normalizedPath = normalizedPath.Substring(1);
        }
        normalizedPath = normalizedPath.Replace('/', Path.DirectorySeparatorChar);
        var dir = Path.GetDirectoryName(normalizedPath);
        if (dir == null) return config;
        var editorConfigPath = FindEditorConfig(dir);
        if (editorConfigPath == null) return config;

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

    private static string? FindEditorConfig(string dir)
    {
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, ".editorconfig");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            var parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent) || parent == dir)
                break;
            dir = parent;
        }

        return null;
    }
}
