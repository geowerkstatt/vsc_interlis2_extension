using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

[TestClass]
public class RenameHandlerTest
{
    private static readonly DocumentUri DocumentA = DocumentUri.From("file:///c:/models/A.ili");
    private static readonly DocumentUri DocumentB = DocumentUri.From("file:///c:/models/B.ili");

    private const string ModelA = """
        INTERLIS 2.4;
        MODEL A AT "http://example.com" VERSION "1" =
            TOPIC Topic =
                CLASS Base =
                    Flag : BOOLEAN;
                END Base;

                VIEW View
                    PROJECTION OF Base;
                    WHERE DEFINED(Base->Flag);
                    =
                    ALL OF Base;
                END View;
            END Topic;
        END A.
        """;

    private const string ModelB = """
        INTERLIS 2.4;
        MODEL B AT "http://example.com" VERSION "1" =
            IMPORTS A;

            TOPIC Topic =
                CLASS Derived EXTENDS A.Topic.Base =
                    Flag (EXTENDED) : BOOLEAN;
                    MANDATORY CONSTRAINT THIS->Flag;
                END Derived;

                CLASS System EXTENDS INTERLIS.REFSYSTEM =
                END System;
            END Topic;
        END B.
        """;

    private static RenameHandler CreateHandler()
    {
        var workspace = TestWorkspace.Open((DocumentA, ModelA), (DocumentB, ModelB));
        return new RenameHandler(workspace.SymbolLookup, workspace.Index, workspace.ExternalFiles, workspace.Selector);
    }

    private static Task<WorkspaceEdit?> Rename(RenameHandler handler, DocumentUri document, int line, int character, string newName)
        => handler.Handle(new RenameParams { TextDocument = new TextDocumentIdentifier(document), Position = new Position(line, character), NewName = newName }, CancellationToken.None);

    private static Task<RangeOrPlaceholderRange?> Prepare(RenameHandler handler, DocumentUri document, int line, int character)
        => handler.Handle(new PrepareRenameParams { TextDocument = new TextDocumentIdentifier(document), Position = new Position(line, character) }, CancellationToken.None);

    /// <summary>
    /// Renders the edits per file as <c>file: line:start-line:end, ...</c> in source order, one file per line, so a
    /// failure shows the actual spans. Every edit is expected to write <paramref name="newName"/>. Compared with
    /// <see cref="AssertEdits"/>, which normalizes the line endings of both sides.
    /// </summary>
    private static string Describe(WorkspaceEdit? edit, string newName)
    {
        Assert.IsNotNull(edit?.Changes);
        return string.Join("\n", edit.Changes
            .OrderBy(change => change.Key.ToString(), StringComparer.Ordinal)
            .Select(change =>
            {
                Assert.IsTrue(change.Value.All(textEdit => textEdit.NewText == newName));
                var ranges = change.Value.Select(e => e.Range).OrderBy(r => r.Start.Line).ThenBy(r => r.Start.Character);
                return $"{Path.GetFileName(change.Key.Path)}: {string.Join(", ", ranges.Select(r => $"{r.Start.Line}:{r.Start.Character}-{r.End.Line}:{r.End.Character}"))}";
            }));
    }

    /// <summary>Asserts the edits rendered by <see cref="Describe"/>, with the line endings of both sides normalized so that the raw string expectations compare on every checkout.</summary>
    private static void AssertEdits(string expected, WorkspaceEdit? edit, string newName)
        => Assert.AreEqual(expected.ReplaceLineEndings(), Describe(edit, newName).ReplaceLineEndings());

    [TestMethod]
    public async Task RenamesTheDeclarationTheUsesAndTheNamesFollowingIt()
    {
        var handler = CreateHandler();

        // From the last name of 'A.Topic.Base' in B (line 5). In A the class is declared twice (lines 3 and 5),
        // and the view over it uses its name as the implicit base name: the token of 'PROJECTION OF Base' (line 8),
        // rewritten once although it declares the base and uses the class, and the uses of the base name in
        // 'Base->Flag' (line 9) and 'ALL OF Base' (line 11).
        var edit = await Rename(handler, DocumentB, 5, 40, "Root");

        AssertEdits(
            """
            A.ili: 3:14-3:18, 5:12-5:16, 8:26-8:30, 9:26-9:30, 11:19-11:23
            B.ili: 5:38-5:42
            """,
            edit, "Root");
    }

    [TestMethod]
    public async Task RenamesAnExtendedRedefinitionWithItsBaseFromEitherEnd()
    {
        var handler = CreateHandler();

        // 'Flag (EXTENDED)' in B (line 6) redefines A's 'Flag' (line 4) by name, so both are renamed, with their uses
        // ('Base->Flag' in A, 'THIS->Flag' in B), whether the rename starts at the base attribute or at the
        // redefinition's use.
        const string expected = """
            A.ili: 4:12-4:16, 9:32-9:36
            B.ili: 6:12-6:16, 7:39-7:43
            """;
        AssertEdits(expected, await Rename(handler, DocumentA, 4, 13, "Active"), "Active");
        AssertEdits(expected, await Rename(handler, DocumentB, 7, 41, "Active"), "Active");
    }

    [TestMethod]
    public async Task PrepareReturnsTheNameUnderTheCursorAndTheNameToBeRenamed()
    {
        var handler = CreateHandler();

        // 'ALL OF Base' names the implicit base, whose rename moves to the class: same name, so the placeholder is
        // the text under the cursor either way.
        var prepared = await Prepare(handler, DocumentA, 11, 21);
        Assert.IsNotNull(prepared);
        Assert.IsTrue(prepared.IsPlaceholderRange);
        Assert.AreEqual(new Range(11, 19, 11, 23), prepared.PlaceholderRange!.Range);
        Assert.AreEqual("Base", prepared.PlaceholderRange.Placeholder);

        Assert.IsNull(await Prepare(handler, DocumentA, 3, 9), "the CLASS keyword names nothing");
    }

    [TestMethod]
    public async Task RejectsNamesTheCompilerWouldNotAccept()
    {
        var handler = CreateHandler();

        foreach (var newName in new[] { "2Base", "CLASS", "New Base", "Neu.Base", "" })
        {
            var exception = await Assert.ThrowsExceptionAsync<RpcErrorException>(() => Rename(handler, DocumentA, 3, 15, newName), newName);
            Assert.AreEqual(ErrorCodes.RequestFailed, exception.Code, newName);
            StringAssert.Contains(exception.Message, "not a valid INTERLIS name");
        }

        // A name already declared next to the element would collide.
        var collision = await Assert.ThrowsExceptionAsync<RpcErrorException>(() => Rename(handler, DocumentA, 3, 15, "View"));
        StringAssert.Contains(collision.Message, "'View' is already declared next to 'Base'");
    }

    [TestMethod]
    public async Task RefusesToRenameWhatWasNeverWrittenDown()
    {
        var handler = CreateHandler();

        // INTERLIS.REFSYSTEM (line 10 of B) belongs to the predefined model.
        var exception = await Assert.ThrowsExceptionAsync<RpcErrorException>(() => Rename(handler, DocumentB, 10, 40, "Reference"));
        StringAssert.Contains(exception.Message, "'REFSYSTEM' is predefined by INTERLIS");
        await Assert.ThrowsExceptionAsync<RpcErrorException>(() => Prepare(handler, DocumentB, 10, 40));

        var nothing = await Assert.ThrowsExceptionAsync<RpcErrorException>(() => Rename(handler, DocumentA, 3, 9, "Root"));
        StringAssert.Contains(nothing.Message, "no element to rename");
    }
}
