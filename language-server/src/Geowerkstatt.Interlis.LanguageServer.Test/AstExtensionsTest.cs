using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.Compiler.AST.Expression;

namespace Geowerkstatt.Interlis.LanguageServer;

[TestClass]
public class AstExtensionsTest
{
    private const string TestModel = """
        INTERLIS 2.4;
        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            TOPIC TestTopic =
                CLASS ClassA =
                    attrA : TEXT*10;
                    MANDATORY CONSTRAINT DEFINED (attrA);
                END ClassA;
            END TestTopic;
        END TestModel.
        """;

    private static ModelDef Model()
    {
        var environment = new InterlisReader().ReadFile(new StringReader(TestModel), "file:///test.ili");
        return (ModelDef)environment.Content["TestModel"];
    }

    [TestMethod]
    public void SelfAndDescendantsCoversContentAndConstraints()
    {
        var names = Model().SelfAndDescendants().Select(d => d.FullyQualifiedName).ToList();

        CollectionAssert.AreEqual(
            new[]
            {
                "TestModel",
                "TestModel.TestTopic",
                "TestModel.TestTopic.ClassA",
                "TestModel.TestTopic.ClassA.attrA",
                "TestModel.TestTopic.ClassA.Constraint1",
            },
            names);
    }

    [TestMethod]
    public void WrittenNamesIncludeTheAssociationQualifyingARole()
    {
        var environment = new InterlisReader().ReadFile(new StringReader("""
            INTERLIS 2.4;
            MODEL Model AT "http://example.com" VERSION "1.0.0" =
                TOPIC Topic =
                    CLASS ClassA =
                    END ClassA;

                    CLASS ClassB =
                        Flag : BOOLEAN;
                    END ClassB;

                    ASSOCIATION Assoc =
                        RoleA -- {0..*} ClassA;
                        RoleB -- {1} ClassB;
                    END Assoc;

                    CONSTRAINTS OF ClassA =
                        MANDATORY CONSTRAINT THIS->RoleB[Assoc]->Flag;
                    END;
                END Topic;
            END Model.
            """), "file:///test.ili");
        var block = ((ModelDef)environment.Content["Model"]).SelfAndDescendants().OfType<ConstraintsBlockDef>().Single();
        var path = ((PathExpression)block.Constraints.OfType<MandatoryConstraint>().Single().Condition!).Reference;

        // Three steps, of which the role carries a fourth written name: its association. The keyword step is written too.
        CollectionAssert.AreEqual(new[] { "THIS", "RoleB", "Assoc", "Flag" }, path.WrittenNames().Select(name => name.Name).ToList());
        Assert.AreEqual(3, path.Path.Count);
    }

    [TestMethod]
    public void NameSourceIsTheElementANameFollows()
    {
        var environment = new InterlisReader().ReadFile(new StringReader("""
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
            """), "file:///test.ili");
        var definitions = ((ModelDef)environment.Content["Model"]).SelfAndDescendants().ToDictionary(d => d.FullyQualifiedName);

        // An EXTENDED attribute or class redefines the inherited element of its name; an implicit base name is the
        // name of the viewable it stands for. Everything else, an explicit alias included, names itself.
        Assert.AreSame(definitions["Model.Topic.Base -> Flag"], definitions["Model.Topic.Derived -> Flag"].NameSource());
        Assert.AreSame(definitions["Model.Topic.Base"], definitions["Model.Extending.Base"].NameSource());
        Assert.AreSame(definitions["Model.Topic.Base"], definitions["Model.Topic.Implicit.Base"].NameSource());
        Assert.IsNull(definitions["Model.Topic.Explicit.Alias"].NameSource());
        Assert.IsNull(definitions["Model.Topic.Base -> Flag"].NameSource());
        Assert.IsNull(definitions["Model.Topic.Base"].NameSource());
    }
}
