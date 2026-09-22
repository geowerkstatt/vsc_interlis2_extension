using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.Compiler.AST.Expression;

namespace Geowerkstatt.Interlis.LanguageServer;

[TestClass]
public class AstExtensionsTest
{
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
}
