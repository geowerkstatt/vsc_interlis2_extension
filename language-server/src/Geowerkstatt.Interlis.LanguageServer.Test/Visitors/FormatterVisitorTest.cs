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
            "    Lorem ipsum dolor sit amet, test." + Environment.NewLine +
            "    Sed do eiusmod tempor incididunt." + Environment.NewLine +
            "    Ut enim ad minim veniam, lorem.";

        var result = FormatterVisitor.SetIndentation(input, 4);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Test()
    {
        var input = """
            !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            !! comment
              !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

                !! Lolo
                !! Lala
                !! Dodo
            INTERLIS /*COM*/2.4;
            /*COM*/
              MODEL
            ModelName
              /*COM*/AT/*COM*/
            "foo.test"
              VERSION
            "123"
              =
            IMPORTS Units; IMPORTS GeometryCHLV95_V2; IMPORTS DMAVTYM_Geometrie_V1_0; IMPORTS DMAVTYM_Topologie_V1_0; IMPORTS DMAVTYM_Vermarkung_V1_0; IMPORTS DMAVTYM_Qualitaet_V1_0; IMPORTS DMAVTYM_Grafik_V1_0;
            CLASS ClassName
            EXTENDS AbstractClassName =
              Property1 : MANDATORY BOOLEAN;
              Property2: text*50;
              END ClassName;
            TOPIC TopicName =
                        BASKET OID AS INTERLIS.UUIDOID;
            OID AS INTERLIS.UUIDOID;
            DEPENDS ON LWB_Nutzungsflaechen_V2_0.LNF_Kataloge, LWB_Bewirtschaftungseinheiten_V2_0.Landw_Betrieb;
            DOMAIN
              Grundstuecksart = (
                Liegenschaft,
                SelbstaendigesDauerndesRecht,
                Bergwerk);
              Mutationsart = (
            	  Normal, 
            	  Projektmutation, 
            	  AbschlussProjektmutation);
              CLASS TopicClassName =
                PropertyA : MANDATORY BOOLEAN;
              END TopicClassName;
              ASSOCIATION Entstehung_Grenzpunkt =
                Entstehung -- {1} GSNachfuehrung;
                entstehender_Grenzpunkt -- {0..*} Grenzpunkt;        
            END Entstehung_Grenzpunkt;
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
