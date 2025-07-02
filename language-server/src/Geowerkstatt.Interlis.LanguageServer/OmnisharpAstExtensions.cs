using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

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

    public static Range ToOmnisharpRange(this Compiler.AST.RangePosition rangePosition)
    {
        return new Range
        {
            Start = rangePosition.Start.ToOmnisharpPosition(),
            End = rangePosition.End.ToOmnisharpPosition()
        };
    }
}
