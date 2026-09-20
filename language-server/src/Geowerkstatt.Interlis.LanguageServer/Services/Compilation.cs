using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Diagnostics;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

/// <summary>
/// The result of compiling a document: the environment holding the document's models and the models they import,
/// and the problems the compiler reported for any of them.
/// </summary>
public sealed record Compilation(InterlisEnvironment Environment, IReadOnlyList<CompilerDiagnostic> Diagnostics);
