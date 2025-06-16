using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Geowerkstatt.Interlis.LanguageServer;

public static class OmnisharpAstExtensions
{
    public static Position ToOmnisharpPosition(this Compiler.AST.Position position)
    {
        return new Position
        {
            Line = position.Line,
            Character = position.Character
        };
    }
}
