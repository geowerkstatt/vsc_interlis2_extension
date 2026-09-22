using Geowerkstatt.Interlis.Compiler.AST;

namespace Geowerkstatt.Interlis.LanguageServer;

/// <summary>
/// Navigation helpers on the compiler's AST.
/// </summary>
internal static class AstExtensions
{
    /// <summary>
    /// Every name <paramref name="reference"/> writes in the source, in source order: its path segments and, for a role
    /// step qualified by its association (<c>Role[Assoc]</c>), the association name too. That qualifier has its own span
    /// and target but is not a step of the path, so a consumer that only walks <see cref="IReference.Path"/> misses it.
    /// A keyword step (<c>THIS</c>, <c>PARENT</c>, ...) is a written name without a target.
    /// </summary>
    public static IEnumerable<PathSegment> WrittenNames(this IReference reference)
        => reference.Path.SelectMany(segment => segment is RolePathSegment role ? new[] { segment, role.Association } : new[] { segment });
}
