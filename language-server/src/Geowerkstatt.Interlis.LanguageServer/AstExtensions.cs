using Geowerkstatt.Interlis.Compiler.AST;

namespace Geowerkstatt.Interlis.LanguageServer;

/// <summary>
/// Navigation helpers on the compiler's AST.
/// </summary>
internal static class AstExtensions
{
    /// <summary>
    /// <paramref name="definition"/> and every definition declared inside it, at any depth: the elements of a
    /// container's content and the constraints of a constraint container, which are held apart from the content
    /// because a constraint name is not part of the namespace.
    /// </summary>
    public static IEnumerable<IInterlisDefinition> SelfAndDescendants(this IInterlisDefinition definition)
    {
        yield return definition;

        if (definition is IInterlisDefinitionContainer container)
        {
            foreach (var descendant in container.Content.Values.SelectMany(child => child.SelfAndDescendants()))
            {
                yield return descendant;
            }
        }

        if (definition is IConstraintContainer constraintContainer)
        {
            foreach (var descendant in constraintContainer.Constraints.SelectMany(constraint => constraint.SelfAndDescendants()))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Every element declared in <paramref name="model"/> that a reference can denote: its definitions (see
    /// <see cref="SelfAndDescendants"/>) and the meta objects its baskets declare (<c>OBJECTS OF Class: Name</c>),
    /// which are <see cref="IReferenceTarget"/>s without being definitions.
    /// </summary>
    public static IEnumerable<IReferenceTarget> ReferenceTargets(this ModelDef model)
    {
        foreach (var definition in model.SelfAndDescendants())
        {
            yield return definition;

            if (definition is MetaDataBasketDef basket)
            {
                foreach (var metaObject in basket.Objects.SelectMany(objects => objects.MetaObjects))
                {
                    yield return metaObject;
                }
            }
        }
    }

    /// <summary>
    /// Every name <paramref name="reference"/> writes in the source, in source order: its path segments and, for a role
    /// step qualified by its association (<c>Role[Assoc]</c>), the association name too. That qualifier has its own span
    /// and target but is not a step of the path, so a consumer that only walks <see cref="IReference.Path"/> misses it.
    /// A keyword step (<c>THIS</c>, <c>PARENT</c>, ...) is a written name without a target.
    /// </summary>
    public static IEnumerable<PathSegment> WrittenNames(this IReference reference)
        => reference.Path.SelectMany(segment => segment is RolePathSegment role ? new[] { segment, role.Association } : new[] { segment });
}
