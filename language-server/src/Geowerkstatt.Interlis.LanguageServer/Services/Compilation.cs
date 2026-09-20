using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

/// <summary>
/// The result of compiling a document: the environment holding the document's models and the models they import,
/// and the problems the compiler reported for any of them.
/// </summary>
public sealed record Compilation(InterlisEnvironment Environment, IReadOnlyList<CompilerDiagnostic> Diagnostics)
{
    /// <summary>
    /// The document's own models, without the imported ones: an environment holding the models whose source is
    /// <paramref name="uri"/>. They are the compiled instances, so their references still point into the imported
    /// models of the full <see cref="Environment"/>.
    /// </summary>
    /// <param name="uri">The document.</param>
    public InterlisEnvironment ForDocument(DocumentUri uri)
    {
        var documentUri = uri.ToString();
        var environment = new InterlisEnvironment { Version = Environment.Version };
        foreach (var model in Environment.Content.Values.Where(model => model.SourceUri == documentUri))
        {
            environment.Content.Add(model.Name, model);
        }

        return environment;
    }
}
