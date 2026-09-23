using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

[TestClass]
public class SymbolLookupTest
{
    private static readonly DocumentUri Document = DocumentUri.From("file:///c:/models/Model.ili");

    private const string TestModel = """
        INTERLIS 2.4;
        MODEL Model AT "http://example.com" VERSION "1.0.0" =
            TOPIC Topic =
                CLASS Base =
                END Base;
            END Topic;

            TOPIC Other =
                CLASS Derived EXTENDS Model.Topic.Base =
                END Derived;
            END Other;
        END Model.
        """;

    private const string ViewModel = """
        INTERLIS 2.4;
        MODEL Model AT "http://example.com" VERSION "1.0.0" =
            TOPIC Topic =
                CLASS ClassA =
                    Flag : BOOLEAN;
                END ClassA;

                VIEW Valid
                    PROJECTION OF ClassA;
                    WHERE DEFINED(ClassA->Flag);
                    =
                    ALL OF ClassA;
                END Valid;
            END Topic;
        END Model.
        """;

    private const string BasketModel = """
        INTERLIS 2.4;
        MODEL Model AT "http://example.com" VERSION "1.0.0" =
            REFSYSTEM BASKET Baskets ~ Systems OBJECTS OF Reference: Bern;

            TOPIC Systems =
                CLASS Reference EXTENDS INTERLIS.REFSYSTEM =
                END Reference;
            END Systems;

            TOPIC Topic =
                CLASS Measurement =
                    Height : 0.00 .. 9999.99 [INTERLIS.m] {Bern};
                    Depth : 0.00 .. 9999.99 [INTERLIS.m] {Baskets.Bern};
                END Measurement;
            END Topic;
        END Model.
        """;

    private const string NamesakeModel = """
        INTERLIS 2.4;
        MODEL Model AT "http://example.com" VERSION "1.0.0" =
            TOPIC Topic =
                CLASS Base =
                    Flag : BOOLEAN;
                END Base;

                CLASS Derived EXTENDS Base =
                    Flag (EXTENDED) : BOOLEAN;
                END Derived;

                VIEW Implicit
                    PROJECTION OF Base;
                    =
                    ALL OF Base;
                END Implicit;

                VIEW Explicit
                    PROJECTION OF Alias ~ Base;
                    =
                    ALL OF Alias;
                END Explicit;
            END Topic;

            TOPIC Extending EXTENDS Topic =
                CLASS Base (EXTENDED) =
                END Base;
            END Extending;
        END Model.
        """;

    /// <summary>Renders locations as <c>line:start-line:end</c>, comma separated, so a failure shows the actual spans.</summary>
    private static string Describe(IEnumerable<Location> locations)
        => string.Join(", ", locations.Select(l => $"{l.Range.Start.Line}:{l.Range.Start.Character}-{l.Range.End.Line}:{l.Range.End.Character}"));

    private static string? Name(IReferenceTarget? target) => (target as IInterlisDefinition)?.FullyQualifiedName ?? target?.Name;

    private static DocumentLookup Lookup(string model = TestModel)
    {
        var environment = new InterlisReader().ReadFile(new StringReader(model), Document.ToString());
        var references = new ReferenceCollectorVisitor().VisitInterlisEnvironment(environment) ?? [];
        return new DocumentLookup(Document, new Compilation(environment, []), references);
    }

    [TestMethod]
    public void FindAtResolvesTheNameUnderThePositionNotTheWholePath()
    {
        var lookup = Lookup();

        // 'CLASS Derived EXTENDS Model.Topic.Base' on line 8: Model at 30, Topic at 36, Base at 42.
        Assert.AreEqual("Model", Name(lookup.FindAt(new Position(8, 32))));
        Assert.AreEqual("Model.Topic", Name(lookup.FindAt(new Position(8, 38))));
        Assert.AreEqual("Model.Topic.Base", Name(lookup.FindAt(new Position(8, 44))));
    }

    [TestMethod]
    public void FindAtOnADeclarationReturnsTheDeclaredElement()
    {
        var lookup = Lookup();

        Assert.AreEqual("Model.Topic.Base", Name(lookup.FindAt(new Position(3, 15))));
        Assert.AreEqual("Model.Other.Derived", Name(lookup.FindAt(new Position(9, 13))), "the name after END");
        Assert.IsNull(lookup.FindAt(new Position(3, 8)), "the CLASS keyword names nothing");
    }

    [TestMethod]
    public void OccurrencesIncludeTheNamesQualifyingAnotherName()
    {
        var lookup = Lookup();

        var topic = lookup.Occurrences(lookup.DeclaredAt(new Position(2, 10))!).Single();
        Assert.AreEqual(Document, topic.Uri);
        Assert.AreEqual(new Range(8, 36, 8, 41), topic.Range);

        var model = lookup.Occurrences(lookup.DeclaredAt(new Position(1, 6))!).Single();
        Assert.AreEqual(new Range(8, 30, 8, 35), model.Range);

        Assert.AreEqual(0, lookup.Occurrences(lookup.DeclaredAt(new Position(8, 14))!).Count(), "a declaration is not an occurrence");
    }

    [TestMethod]
    public void DeclarationLocationsAreTheOpeningNameThenTheNameAfterEnd()
    {
        var lookup = Lookup();
        var derived = lookup.FindAt(new Position(8, 14))!;

        var locations = SymbolLookup.DeclarationLocations(derived).ToList();

        CollectionAssert.AreEqual(new[] { new Range(8, 14, 8, 21), new Range(9, 12, 9, 19) }, locations.Select(l => l.Range).ToList());
        Assert.IsTrue(locations.All(l => l.Uri == Document));
    }
    [TestMethod]
    public void AMetaObjectDeclaredByABasketIsAnElementLikeAnyOther()
    {
        var lookup = Lookup(BasketModel);

        // '{Bern}' (line 11) and '{Baskets.Bern}' (line 12) both denote the meta object the basket declares on line 2,
        // which is a reference target without being a definition.
        var metaObject = lookup.FindAt(new Position(11, 53));
        Assert.IsInstanceOfType<MetaObjectDeclaration>(metaObject);
        Assert.AreEqual("Bern", metaObject!.Name);
        Assert.AreSame(metaObject, lookup.DeclaredAt(new Position(2, 64)));

        Assert.AreEqual("2:61-2:65", Describe(SymbolLookup.DeclarationLocations(metaObject)));
        Assert.AreEqual("11:51-11:55, 12:58-12:62", Describe(lookup.Occurrences(metaObject)));
        CollectionAssert.AreEqual(new[] { "Model" }, lookup.DeclaringModels(metaObject).Select(m => m.Name).ToList());
    }

    [TestMethod]
    public void AnImplicitBaseNameOfAViewIsDeclaredByTheViewableReference()
    {
        var lookup = Lookup(ViewModel);

        // 'ALL OF ClassA' (line 11) and the first step of 'ClassA->Flag' (line 9) name the view's base, not the class.
        var allOf = lookup.FindAt(new Position(11, 21));
        var firstStep = lookup.FindAt(new Position(9, 28));
        Assert.IsInstanceOfType<BaseView>(allOf);
        Assert.AreSame(allOf, firstStep);

        // Their declaration is the token of 'PROJECTION OF ClassA' (line 8), which is also a reference to the class:
        // from there go-to-definition follows the reference to the class, find-references starts at the declared base.
        CollectionAssert.AreEqual(new[] { new Range(8, 26, 8, 32) }, SymbolLookup.DeclarationLocations(allOf!).Select(l => l.Range).ToList());
        Assert.IsInstanceOfType<ClassDef>(lookup.FindAt(new Position(8, 28)));
        Assert.IsInstanceOfType<ClassDef>(lookup.ReferencedAt(new Position(8, 28)));
        Assert.AreSame(allOf, lookup.DeclaredAt(new Position(8, 28)));
    }

    [TestMethod]
    public void NameRangeAtIsTheSpanOfTheNameUnderThePosition()
    {
        var lookup = Lookup();

        Assert.AreEqual(new Range(8, 36, 8, 41), lookup.NameRangeAt(new Position(8, 38)), "a name qualifying another one");
        Assert.AreEqual(new Range(3, 14, 3, 18), lookup.NameRangeAt(new Position(3, 15)), "a declaration");
        Assert.AreEqual(new Range(9, 12, 9, 19), lookup.NameRangeAt(new Position(9, 19)), "the name after END, from its end");
        Assert.IsNull(lookup.NameRangeAt(new Position(3, 8)), "the CLASS keyword names nothing");
    }

    [TestMethod]
    public void NamedAfterListsTheElementsThatFollowAnotherElementsName()
    {
        var lookup = Lookup(NamesakeModel);
        var baseClass = lookup.DeclaredAt(new Position(3, 15))!;
        var flag = lookup.DeclaredAt(new Position(4, 13))!;

        // The implicit base name of the view over Base and the EXTENDED class of the extending topic follow the
        // class's name; the explicit alias chooses its own. The EXTENDED attribute follows the base attribute.
        CollectionAssert.AreEqual(new[] { "Model.Topic.Implicit.Base", "Model.Extending.Base" }, lookup.NamedAfter(baseClass).Select(d => d.FullyQualifiedName).ToList());
        CollectionAssert.AreEqual(new[] { "Model.Topic.Derived -> Flag" }, lookup.NamedAfter(flag).Select(d => d.FullyQualifiedName).ToList());
        Assert.AreEqual(0, lookup.NamedAfter(lookup.DeclaredAt(new Position(18, 27))!).Count(), "nothing follows the alias");
    }
}
