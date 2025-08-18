using Antlr4.Runtime;
using Geowerkstatt.Interlis.Compiler;
using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class FormatterVisitorTest
{
    [TestMethod]
    public void GetDefaultFormattingTest()
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

        var expected = """
            INTERLIS 2.4; MODEL ModelName AT "foo.test" VERSION "123" =
            /* Don't care   about syntax */
            (lolo) [lala] {lulu} Tis, is, a, list; END ModelName.
            """;

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var reader = new InterlisReader(loggerFactory);

        // token stream from Lexer
        var tokenStream = reader.RunLexer(new StringReader(input));
        tokenStream.Fill();

        var formatter = new FormatterVisitor(tokenStream);
        var result = formatter.GetDefaultFormatting(0, tokenStream.Size - 1);

        Assert.AreEqual(expected, result.Content);
    }

    [DataTestMethod]
    [DataRow(
        """
        INTERLIS 2.4;

        """,
        """INTERLIS  2.4;""",
        DisplayName = "Add newline at end of file"
    )]
    [DataRow(
        """
        !!!!!

        !! comment
        !! comment

        !! comment

        INTERLIS 2.4;

        """,
        """

        !!!!!

        !! comment
        !! comment

        !! comment

        INTERLIS 2.4;
        """,
        DisplayName = "Keep empty line between comments"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        !!@ color = #ff0000; font = "bold italic";
        MODEL A
        AT "URL" VERSION "1" =
        END A.

        """,
        """
        INTERLIS 2.4;
        !!@color=#ff0000;font="bold italic";
        MODEL A AT"URL"VERSION"1"=END A.
        """,
        DisplayName = "meta comment"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M (en)
        AT "URL" VERSION "1" =

          TOPIC T = /**
          * wtf */

            /**
              * Awesome class
               */
            CLASS A =
              /**
              * A text attribute
              */
              Text: TEXT*12; !! comment
              /**
               * A number attribute
               */
              Number: 0 .. 1000 [m];
              /* multi line block comment
              */ Bool: BOOLEAN;
            /**
             * After attribute Doc-Comment
             */
            END A;

          END T;

        END M.

        """,
        """
        INTERLIS 2.4;MODEL M(en)AT"URL"VERSION"1"=TOPIC T=/**
        * wtf */
        /**
          * Awesome class
           */
        CLASS A=
            /**
          * A text attribute
        */
        Text:TEXT*12; !! comment

            /**
             * A number attribute
             */Number:0..1000[m];
             /* multi line block comment
             */Bool : BOOLEAN;
        /**
         * After attribute Doc-Comment
         */
        END A;END T;END M. 
        """,
        DisplayName = "doc comment"
    )]
    [DataRow(
        """
        INTERLIS /*1*/ 2.4 /*2*/ ; /*81*/

        MODEL /*3*/ A /*4*/ ( /*5*/ en /*6*/ ) /*7*/
        AT /*8*/ "URL" /*9*/ VERSION /*10*/ "1" /*11*/ = /*12*/
          IMPORTS /*13*/ Units /*14*/ , /*16*/ UNQUALIFIED /*15*/ INTERLIS /*17*/ ; /*18*/
          IMPORTS /*19*/ GeometryCHLV95_V2 /*20*/ ; /*50*/

          DOMAIN /*49*/
            NestedEnum /*21*/ = /*47*/ ( /*29*/
              red /*28*/ ( /*22*/ lightred /*23*/ , /*24*/ darkred /*25*/ : /*26*/ FINAL /*27*/ ) /*30*/ , /*41*/
              green /*40*/ ( /*31*/
                forest /*32*/ , /*36*/
                watermelon /*35*/ ( /*33*/ FINAL /*34*/ ) /*37*/ , /*38*/
                salad /*39*/ ) /*42*/ , /*43*/
              blue /*44*/ : /*45*/ FINAL /*46*/ ) /*48*/ ; /*77*/

          TOPIC /*51*/ T /*52*/ = /*73*/

            CLASS /*53*/ A /*54*/ = /*60*/
              Text /*55*/ : /*58*/ TEXT /*56*/* /*57*/12 /*59*/ ; /*69*/
              Number /*61*/ : /*67*/ 0 /*62*/ .. /*63*/ 1000 /*64*/ [ /*65*/ m /*66*/ ] /*68*/ ; /*70*/
            END /*71*/ A /*72*/ ; /*74*/

          END /*75*/ T /*76*/ ; /*78*/

        END /*79*/ A /*80*/ . /*82*/

        """,
        """INTERLIS/*1*/2.4/*2*/;/*81*/MODEL/*3*/A/*4*/(/*5*/en/*6*/)/*7*/AT/*8*/"URL"/*9*/VERSION/*10*/"1"/*11*/=/*12*/IMPORTS/*13*/Units/*14*/,/*16*/UNQUALIFIED/*15*/INTERLIS/*17*/;/*18*/IMPORTS/*19*/GeometryCHLV95_V2/*20*/;/*50*/DOMAIN/*49*/NestedEnum/*21*/=/*47*/(/*29*/red/*28*/(/*22*/lightred/*23*/,/*24*/darkred/*25*/:/*26*/FINAL/*27*/)/*30*/,/*41*/green/*40*/(/*31*/forest/*32*/,/*36*/watermelon/*35*/(/*33*/FINAL/*34*/)/*37*/,/*38*/salad/*39*/)/*42*/,/*43*/blue/*44*/:/*45*/FINAL/*46*/)/*48*/;/*77*/TOPIC/*51*/T/*52*/=/*73*/CLASS/*53*/A/*54*/=/*60*/Text/*55*/:/*58*/TEXT/*56*/*/*57*/12/*59*/;/*69*/Number/*61*/:/*67*/0/*62*/../*63*/1000/*64*/[/*65*/m/*66*/]/*68*/;/*70*/END/*71*/A/*72*/;/*74*/END/*75*/T/*76*/;/*78*/END/*79*/A/*80*/./*82*/""",
        DisplayName = "comments everywhere"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL A
        AT "URL" VERSION "1" =
        END A.

        MODEL B
        AT "URL" VERSION "1" =
        END B.

        """,
        """INTERLIS 2.4;MODEL A AT"URL"VERSION"1"=END A.MODEL B AT"URL"VERSION"1"=END B.""",
        DisplayName = "Empty model definition"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL A (en)
        AT "URL" VERSION "1" =

          TOPIC T =
          END T;

          TOPIC P =
          END P;

        END A.

        """,
        """INTERLIS 2.4;MODEL A(en)AT"URL"VERSION"1"=TOPIC T=END T;TOPIC P=END P;END A.""",
        DisplayName = "Model with topics"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL A
        AT "URL" VERSION "1" =
          IMPORTS Units, UNQUALIFIED INTERLIS;
          IMPORTS GeometryCHLV95_V2;

        END A.

        """,
        """INTERLIS 2.4;MODEL A AT"URL"VERSION"1"=IMPORTS Units,UNQUALIFIED INTERLIS;IMPORTS GeometryCHLV95_V2;END A.""",
        DisplayName = "Model with imports"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          TOPIC T =

            CLASS A =
            END A;

            CLASS B =
            END B;

          END T;

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=TOPIC T=CLASS A=END A;CLASS B=END B;END T;END M.""",
        DisplayName = "Model with topic and classes"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          TOPIC T
          EXTENDS Base.T =
            BASKET OID AS INTERLIS.UUIDOID;
            OID AS INTERLIS.UUIDOID;
            DEPENDS ON Base.A, Base.B;

          END T;

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=TOPIC T EXTENDS Base.T=BASKET OID AS INTERLIS.UUIDOID;OID AS INTERLIS.UUIDOID;DEPENDS ON Base.A,Base.B;END T;END M.""",
        DisplayName = "topic with extends, OID and depends"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          CLASS C (ABSTRACT)
          EXTENDS Base.C =
            OID AS INTERLIS.UUIDOID;
          ATTRIBUTE
            Text: TEXT*12;
            Number: 0 .. 1000 [m];
            UNIQUE constrName: Some->Attr->Path;
            MANDATORY CONSTRAINT Boolean OR Enum == #red;
          PARAMETER
            Count: 0 .. 1000;
          END C;

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=CLASS C(ABSTRACT)EXTENDS Base.C=OID AS INTERLIS.UUIDOID;ATTRIBUTE Text:TEXT*12;Number:0..1000[m];UNIQUE constrName:Some->Attr->Path;MANDATORY CONSTRAINT Boolean OR Enum==#red;PARAMETER Count:0..1000;END C;END M.""",
        DisplayName = "class def"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          CLASS C (ABSTRACT) =
          ATTRIBUTE
            Text: TEXT*12;
          END C;

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=CLASS C(ABSTRACT)=ATTRIBUTE Text:TEXT*12;END C;END M.""",
        DisplayName = "class def nothing between EQUAL_SIGN and ATTRIBUTE keyword"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          TOPIC T =

            ASSOCIATION A
            EXTENDS Base.A =
              OID AS INTERLIS.UUIDOID;
              Documentation -- {1} Document;
              Event -- {0..*} Event;
            ATTRIBUTE
              Important: INTERLIS.BOOLEAN; CARDINALITY = {0..*};
              CONSTRAINT plausible: <= 50 % Important;
            END A;

          END T;

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=TOPIC T=ASSOCIATION A EXTENDS Base.A=OID AS INTERLIS.UUIDOID;Documentation--{1}Document;Event--{0..*}Event;ATTRIBUTE Important:INTERLIS.BOOLEAN;CARDINALITY={0..*};CONSTRAINT plausible:<=50%Important;END A;END T;END M.""",
        DisplayName = "association def"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          TOPIC T =

            CONSTRAINTS OF Class =
              MANDATORY CONSTRAINT (NOT (DEFINED (Nutzungsart->Reference->Gueltig_Von)) OR Nutzungsart->Reference->Gueltig_Von <= Bezugsjahr->Bezugsjahr) AND (NOT (DEFINED (Nutzungsart->Reference->Gueltig_Bis)) OR Nutzungsart->Reference->Gueltig_Bis >= Bezugsjahr->Bezugsjahr);
            END;

          END T;

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=TOPIC T=CONSTRAINTS OF Class=MANDATORY CONSTRAINT(NOT(DEFINED(Nutzungsart->Reference->Gueltig_Von))OR Nutzungsart->Reference->Gueltig_Von<=Bezugsjahr->Bezugsjahr)AND(NOT(DEFINED(Nutzungsart->Reference->Gueltig_Bis))OR Nutzungsart->Reference->Gueltig_Bis>=Bezugsjahr->Bezugsjahr);END;END T;END M.""",
        DisplayName = "constraints def"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          DOMAIN
            text = TEXT*12;

            bracketText = TEXT*50
              CONSTRAINTS
                prefix: startsWith (THIS, "["),
                postfix: endsWith (THIS, "]");

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=DOMAIN text=TEXT*12;bracketText=TEXT*50 CONSTRAINTS prefix:startsWith(THIS,"["),postfix:endsWith(THIS,"]");END M.""",
        DisplayName = "domain text"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          DOMAIN
            SimpleEnum = (red, green);

            LargeEnum = (
              red,
              green,
              blue,
              yellow);

            NestedEnum = (
              red (lightred, darkred: FINAL),
              green (
                forest,
                watermelon (FINAL),
                salad),
              blue: FINAL);

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=DOMAIN SimpleEnum=(red,green);LargeEnum=(red,green,blue,yellow);NestedEnum=(red(lightred,darkred:FINAL),green(forest,watermelon(FINAL),salad),blue:FINAL);END M.""",
        DisplayName = "domain enum"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          DOMAIN
            Coord2 = COORD
              2410000.000 .. 2920000.000 [INTERLIS.m] {CHLV95[1]},
              995000.000 .. 1360000.000 [INTERLIS.m] {CHLV95[2]},
              ROTATION 2 -> 1;

            Surface = SURFACE WITH (STRAIGHTS, ARCS) VERTEX Coord2 WITHOUT OVERLAPS > 0.002;

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=DOMAIN Coord2=COORD 2410000.000..2920000.000[INTERLIS.m]{CHLV95[1]},995000.000..1360000.000[INTERLIS.m]{CHLV95[2]},ROTATION 2->1;Surface=SURFACE WITH(STRAIGHTS,ARCS)VERTEX Coord2 WITHOUT OVERLAPS>0.002;END M.""",
        DisplayName = "domain geometry"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          TOPIC T =

            VIEW InspectionOf
              INSPECTION OF ClassAlias ~ Model.Topic.Class->Attr;
              =
              ATTRIBUTE
              ALL OF ClassAlias;
              CalculatedAttr := PARENT->SomeAttr->SomeAttr;
              MANDATORY CONSTRAINT Attr > 50;
            END InspectionOf;

            VIEW ProjectionWithWhere
              PROJECTION OF Class;
              WHERE DEFINED (Class->Attr_A) AND DEFINED (Class->Attr_B);
              =
              ALL OF Class;
              UNIQUE constr: Attr_A, Attr_B;
              UNIQUE constr2: Attr_C;
            END ProjectionWithWhere;

          END T;

        END M.

        """,
        """
        INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=TOPIC T=VIEW InspectionOf INSPECTION OF ClassAlias~Model.Topic.Class->Attr;=ATTRIBUTE ALL OF ClassAlias;CalculatedAttr:=PARENT->SomeAttr->SomeAttr;MANDATORY CONSTRAINT Attr>50;END InspectionOf;
        VIEW ProjectionWithWhere PROJECTION OF Class;WHERE DEFINED(Class->Attr_A)AND DEFINED(Class->Attr_B);=ALL OF Class;UNIQUE constr:Attr_A,Attr_B;UNIQUE constr2:Attr_C;END ProjectionWithWhere;END T;END M.
        """,
        DisplayName = "view def"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M
        AT "URL" VERSION "1" =

          REFSYSTEM BASKET BCoordSys ~ CoordSys.CoordsysTopic
            OBJECTS OF GeoCartesian2D: CHLV95
            OBJECTS OF GeoHeight: SwissOrthometricAlt;

        END M.

        """,
        """INTERLIS 2.4;MODEL M AT"URL"VERSION"1"=REFSYSTEM BASKET BCoordSys~CoordSys.CoordsysTopic OBJECTS OF GeoCartesian2D:CHLV95 OBJECTS OF GeoHeight:SwissOrthometricAlt;END M.""",
        DisplayName = "metadata basket def"
    )]
    [DataRow(
        """
        INTERLIS 2.4;

        MODEL M /* url and version missing */ =
          CLASS C =
          /* comment */
          Attr: TEXT * 12;
          Attr2: BOOLEAN;

        END C;
        END M.

        """,
        """
        INTERLIS 2.4;
        MODEL M /* url and version missing */ = CLASS C = 
        /* comment */
        Attr:TEXT*12;Attr2:BOOLEAN;END C;
        END M.
        """,
        DisplayName = "interlis with syntax errors"
    )]
    [DataRow(
        """
        Party 🎉 Input

        """,
        """Party 🎉 Input""",
        DisplayName = "interlis with lexer unexpected input"
    )]
    public void InterlisFormatting(string expected, string input)
    {
        AssertFormatting(expected, input, parser => parser.interlis());
    }

    private void AssertFormatting(string expected, string input, Func<Interlis24Parser, ParserRuleContext> parseRule)
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var reader = new InterlisReader(loggerFactory);

        // token stream from Lexer
        var tokenStream = reader.RunLexer(new StringReader(input));

        // parse tree from parser
        var interlisParser = reader.GetParser(tokenStream);
        var parseTree = parseRule(interlisParser);

        var formatter = new FormatterVisitor(tokenStream);
        var formattedOutput = parseTree.Accept(formatter);

        Console.WriteLine(parseTree.Accept(new MinifyVisitor()).Content);

        Assert.AreEqual(expected, formattedOutput.Content);
    }
}
