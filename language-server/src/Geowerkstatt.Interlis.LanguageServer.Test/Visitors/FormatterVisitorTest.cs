using Geowerkstatt.Interlis.Compiler;
using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class FormatterVisitorTest
{
    [TestMethod]
    public void NormalizeSpacesTest()
    {
        var input = """
            INTERLIS     2.4;
            MODEL
            ModelName AT    "foo.test"VERSION   "123"=
                /* Don't care   about syntax */
                    (lolo)  [   lala ] {
            lulu}
                Tis,is
               , a,        list     ;
              END   ModelName.
            """;
        var expected = "INTERLIS 2.4; MODEL ModelName AT \"foo.test\" VERSION \"123\" = /* Don't care   about syntax */ (lolo) [lala] {lulu} Tis, is, a, list; END ModelName.";

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var reader = new InterlisReader(loggerFactory);

        // token stream from Lexer
        var tokenStream = reader.RunLexer(new StringReader(input));
        tokenStream.Fill();

        var formatter = new FormatterVisitor(loggerFactory, tokenStream);
        var result = formatter.GetSpacesNormalizedString(0, tokenStream.Size - 1);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void SetIndentationTest()
    {
        var input =
            "  Lorem ipsum dolor sit amet, test.\r\n" +
            "   \t     Sed do eiusmod tempor incididunt.   \n" +
            "Ut enim ad minim veniam, lorem.";

        var expected =
            "    Lorem ipsum dolor sit amet, test.\r\n" +
            "    Sed do eiusmod tempor incididunt.\r\n" +
            "    Ut enim ad minim veniam, lorem.";

        var result = FormatterVisitor.SetIndentation(input, 4);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Test()
    {
        var input = """
              !! comment
            INTERLIS /*COM*/2.4;
            /*COM*/
              MODEL
            ModelName
              /*COM*/AT/*COM*/
            "foo.test"
              VERSION
            "123"
              =
            CLASS ClassName =
              END ClassName;
            TOPIC TopicName =
              CLASS TopicClassName =
            END TopicClassName;
              END TopicName;
                /*COM*/
            END ModelName.
            /*COM*/
            """;

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var reader = new InterlisReader(loggerFactory);

        // token stream from Lexer
        var tokenStream = reader.RunLexer(new StringReader(input));

        // parse tree from parser
        var interlisParser = reader.GetParser(tokenStream);
        var parseTree = interlisParser.interlis();

        var formatter = new FormatterVisitor(loggerFactory, tokenStream);
        var formattedOutput = formatter.VisitInterlis(parseTree);

        
    }
}
