using Geowerkstatt.Interlis.Compiler.AST;
using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer.Diagnostics;

/// <summary>
/// A problem the compiler reported while compiling a document: the log level, the message and the range it refers
/// to, whose <see cref="RangePosition.SourceUri"/> names the file it lies in.
/// </summary>
public sealed record CompilerDiagnostic(LogLevel Level, string Message, RangePosition Range);
