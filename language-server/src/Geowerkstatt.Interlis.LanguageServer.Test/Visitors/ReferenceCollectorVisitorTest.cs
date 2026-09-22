using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class ReferenceCollectorVisitorTest
{
    private const string TestModelAssociation = """
        INTERLIS 2.4;

        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            TOPIC TestTopic =
                CLASS ClassA =
                    attrA: TEXT*10;
                END ClassA;

                CLASS ClassB =
                    attrB: 10..20;
                END ClassB;

                ASSOCIATION Assoc1 =
                    AssocA -- {0..*} ClassA;
                    AssocB -<> {1} ClassB;
                END Assoc1;
            END TestTopic;
        END TestModel.
        """;

    private static List<ReferenceDefinition> Collect(string model)
    {
        var interlisFile = new InterlisReader().ReadFile(new StringReader(model), "file://test.ili");
        var references = new ReferenceCollectorVisitor().VisitInterlisEnvironment(interlisFile);

        Assert.IsNotNull(references);
        return references;
    }

    /// <summary>
    /// Renders the occurrences in source order as <c>Target@line:start-end</c>, one per line, with zero-based
    /// positions. The visitor emits them in visit order, which is not source order: a reference is registered on the
    /// container that writes it, and a container's references are visited before its content.
    /// </summary>
    private static string Describe(IEnumerable<ReferenceDefinition> occurrences)
        => string.Join("\n", occurrences
            .OrderBy(occurrence => occurrence.OccurenceStart)
            .Select(occurrence => $"{(occurrence.Target as IInterlisDefinition)?.FullyQualifiedName ?? occurrence.Target.Name}@{occurrence.OccurenceStart.Line}:{occurrence.OccurenceStart.Character}-{occurrence.OccurenceEnd.Character}"));

    [TestMethod]
    public void TestInterlisFile()
    {
        var references = Collect(TestModelAssociation);

        CollectionAssert.AreEqual(new[] { "ClassA", "ClassB" }, references.Select(r => r.Target.Name).ToList());
        Assert.IsTrue(references.All(r => r.OccurenceFile == new Uri("file://test.ili")));
    }

    private const string TestModelImports = """
        INTERLIS 2.4;

        MODEL ImportedModel (de) AT "http://models.geow.cloud" VERSION "1" =
        END ImportedModel.

        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            IMPORTS ImportedModel;

            TOPIC TestTopic =
                CLASS ClassA =
                    attrA: TEXT*10;
                END ClassA;

                CLASS ClassB =
                    attrB: 10..20;
                END ClassB;

                ASSOCIATION Assoc1 =
                    AssocA -- {0..*} ClassA;
                    AssocB -<> {1} ClassB;
                END Assoc1;
            END TestTopic;
        END TestModel.
        """;

    [TestMethod]
    public void TestInterlisImports()
    {
        var references = Collect(TestModelImports);

        CollectionAssert.AreEqual(new[] { "ImportedModel", "ClassA", "ClassB" }, references.Select(r => r.Target.Name).ToList());
    }

    [TestMethod]
    public void EveryNameOfAQualifiedReferenceIsItsOwnOccurrence()
    {
        var references = Collect("""
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
            """);

        // Model.Topic.Base is one reference to the class, but three written names; the position on 'Topic' has to
        // resolve to the topic, and a search for the topic has to find it here.
        Assert.AreEqual(
            """
            Model@8:30-35
            Model.Topic@8:36-41
            Model.Topic.Base@8:42-46
            """,
            Describe(references));
    }

    [TestMethod]
    public void ObjectPathsYieldTheirStepsAndRoleQualifiersButNoKeywords()
    {
        var references = Collect("""
            INTERLIS 2.4;
            MODEL Model AT "http://example.com" VERSION "1.0.0" =
                TOPIC Topic =
                    CLASS ClassA =
                        Flag : BOOLEAN;
                        MANDATORY CONSTRAINT THIS->Flag;
                    END ClassA;

                    CLASS ClassB =
                        Flag : BOOLEAN;
                    END ClassB;

                    ASSOCIATION Assoc =
                        RoleA -- {0..*} ClassA;
                        RoleB -- {1} ClassB;
                    END Assoc;

                    CONSTRAINTS OF ClassA =
                        MANDATORY CONSTRAINT RoleB[Assoc]->Flag;
                    END;
                END Topic;
            END Model.
            """);

        // THIS denotes the context object, not a definition, so it is no occurrence. The association qualifying a
        // role is a written name in its own right, between the role and the next step. An attribute or role is
        // qualified with -> by its class, the compiler's convention for members.
        Assert.AreEqual(
            """
            Model.Topic.ClassA -> Flag@5:39-43
            Model.Topic.ClassA@13:28-34
            Model.Topic.ClassB@14:25-31
            Model.Topic.ClassA@17:23-29
            Model.Topic.Assoc -> RoleB@18:33-38
            Model.Topic.Assoc@18:39-44
            Model.Topic.ClassB -> Flag@18:47-51
            """,
            Describe(references));
    }
}
