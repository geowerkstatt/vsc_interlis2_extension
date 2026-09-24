using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

[TestClass]
public class HoverHandlerTest
{
    private static readonly DocumentUri DocumentA = DocumentUri.From("file:///c:/models/A.ili");
    private static readonly DocumentUri DocumentB = DocumentUri.From("file:///c:/models/B.ili");

    private const string ModelA = """
        INTERLIS 2.4;
        /** The catalogue model.
         */
        MODEL A (de) AT "http://example.com/?v=1" VERSION "1" =
            TOPIC Topic =
                /** A catalogue entry.
                 *
                 * **Never** deleted, only *closed*:
                 * - closed entries stay referenced
                 */
                CLASS Base (ABSTRACT)
                    EXTENDS INTERLIS.REFSYSTEM =
                    /** Whether the entry is still valid. */
                    Flag : MANDATORY BOOLEAN;
                    Name : TEXT*20;
                END Base;

                DOMAIN
                    /** A year of the Gregorian calendar. */
                    Year = 1900 .. 2100;
            END Topic;
        END A.
        """;

    private const string ModelB = """
        INTERLIS 2.4;
        MODEL B AT "http://example.com" VERSION "1" =
            IMPORTS A;

            TOPIC Topic =
                CLASS Derived EXTENDS A.Topic.Base =
                    MANDATORY CONSTRAINT THIS->Flag;
                END Derived;
            END Topic;
        END B.
        """;

    private static HoverHandler CreateHandler()
    {
        var workspace = TestWorkspace.Open((DocumentA, ModelA), (DocumentB, ModelB));
        return new HoverHandler(workspace.SymbolLookup, workspace.Selector);
    }

    /// <summary>A name in code style linked to its declaration in <paramref name="document"/> at the one-based line and column.</summary>
    private static string Link(string name, DocumentUri document, int line, int column) => $"[`{name}`](<{document}#L{line},{column}>)";

    /// <summary>
    /// The hover's range and markdown, the latter with the line endings of this source file so that the raw string
    /// expectations compare on every checkout (the handler always writes <c>\n</c>).
    /// </summary>
    private static async Task<(Range? Range, string Markdown)?> Hover(HoverHandler handler, DocumentUri document, int line, int character)
    {
        var hover = await handler.Handle(new HoverParams { TextDocument = new TextDocumentIdentifier(document), Position = new Position(line, character) }, CancellationToken.None);
        return hover == null ? null : (hover.Range, hover.Contents.MarkupContent!.Value.ReplaceLineEndings());
    }

    [TestMethod]
    public async Task ShowsTheQualifiedNameAndTheDocComments()
    {
        var handler = CreateHandler();

        // From B's 'A.Topic.Base' (line 5): the class's qualified name with the model, the topic and the class each
        // linked to their declaration in A, then its doc comment without delimiters and continuation stars, blank
        // lines kept. Only the continuation star goes, not the stars of an emphasis.
        var hover = await Hover(handler, DocumentB, 5, 40);

        Assert.IsNotNull(hover);
        Assert.AreEqual(new Range(5, 38, 5, 42), hover.Value.Range);
        Assert.AreEqual(
            $"""
            {Link("A", DocumentA, 4, 7)}.{Link("Topic", DocumentA, 5, 11)}.{Link("Base", DocumentA, 11, 15)}

            A catalogue entry.

            **Never** deleted, only *closed*:
            - closed entries stay referenced

            """.ReplaceLineEndings(),
            hover.Value.Markdown);

        // 'THIS->Flag' in B (line 6) names the attribute inherited from A, separated from its class as the compiler
        // qualifies attributes; its doc comment is on one line.
        Assert.AreEqual(
            $"""
            {Link("A", DocumentA, 4, 7)}.{Link("Topic", DocumentA, 5, 11)}.{Link("Base", DocumentA, 11, 15)} -> {Link("Flag", DocumentA, 14, 13)}

            Whether the entry is still valid.

            """.ReplaceLineEndings(),
            (await Hover(handler, DocumentB, 6, 40))!.Value.Markdown);

        // The import 'A' in B (line 2) names the model, which is its own qualified name.
        Assert.AreEqual($"{Link("A", DocumentA, 4, 7)}\n\nThe catalogue model.\n".ReplaceLineEndings(), (await Hover(handler, DocumentB, 2, 13))!.Value.Markdown);
    }

    [TestMethod]
    public async Task ShowsOnlyTheNameWithoutDocComments()
    {
        var handler = CreateHandler();

        Assert.AreEqual($"{Link("A", DocumentA, 4, 7)}.{Link("Topic", DocumentA, 5, 11)}.{Link("Base", DocumentA, 11, 15)} -> {Link("Name", DocumentA, 15, 13)}\n".ReplaceLineEndings(), (await Hover(handler, DocumentA, 14, 13))!.Value.Markdown, "an attribute without doc comment");
        Assert.AreEqual("`INTERLIS`.`REFSYSTEM`\n".ReplaceLineEndings(), (await Hover(handler, DocumentA, 11, 32))!.Value.Markdown, "an element of the predefined model, which was never written down, has nothing to link to");
    }

    [TestMethod]
    public async Task ShowsNothingWhereNoNameIsWritten()
    {
        var handler = CreateHandler();

        Assert.IsNull(await Hover(handler, DocumentA, 10, 10), "the CLASS keyword names nothing");
    }
}
