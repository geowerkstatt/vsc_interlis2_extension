using Geowerkstatt.Interlis.Compiler.AST;

namespace Geowerkstatt.Interlis.LanguageServer;

/// <summary>
/// Helpers to read the target of a <see cref="RestrictedRef"/>, which is either a
/// reference to a definition or one of the ANYCLASS/ANYSTRUCTURE keywords.
/// </summary>
public static class RestrictedRefExtensions
{
    /// <summary>
    /// Gets the display name of the reference target: the last segment of the referenced path,
    /// or the keyword for ANYCLASS/ANYSTRUCTURE.
    /// </summary>
    public static string? GetTargetName(this RestrictedRef.RefTarget? target)
    {
        return target switch
        {
            RestrictedRef.DefinitionRef definitionRef => definitionRef.Reference?.Path.LastOrDefault(),
            RestrictedRef.AnyRef anyRef => anyRef.Kind switch
            {
                RestrictedRef.AnyKind.Class => "ANYCLASS",
                RestrictedRef.AnyKind.Structure => "ANYSTRUCTURE",
                _ => null,
            },
            _ => null,
        };
    }

    /// <summary>
    /// Gets the resolved definition the reference points to, or <c>null</c> for ANYCLASS/ANYSTRUCTURE
    /// and for unresolved references.
    /// </summary>
    public static IReferenceTarget? GetTargetDefinition(this RestrictedRef.RefTarget? target)
    {
        return (target as RestrictedRef.DefinitionRef)?.Reference?.Target;
    }
}
