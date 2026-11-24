using Geowerkstatt.Interlis.Compiler.AST.Types;
using System.Globalization;

namespace Geowerkstatt.Interlis.LanguageServer;

/// <summary>
/// Display helpers for the compiler's <see cref="NumericType"/>.
/// </summary>
internal static class NumericTypeExtensions
{
    /// <summary>
    /// The type as documentation shows it: its range (see <see cref="FormatRange"/>), or
    /// <paramref name="boundlessLabel"/> for a <c>NUMERIC</c> without bounds, followed by its unit
    /// (<c>0.0..5.0 [m]</c> for <c>0.0 .. 5.0 [INTERLIS.m]</c>).
    /// </summary>
    public static string Format(this NumericType numericType, string boundlessLabel)
    {
        var text = numericType.FormatRange() ?? boundlessLabel;
        var unitName = numericType.Unit?.Target?.Name ?? numericType.Unit?.Path.LastOrDefault()?.Name;
        return string.IsNullOrEmpty(unitName) ? text : $"{text} [{unitName}]";
    }

    /// <summary>
    /// The range <c>min..max</c> in the notation the model declares (RefHB 3.8.5). The bounds are stored as
    /// <see cref="double"/>, which drops the Stellenzahl, so it is restored from the type: decimal bounds keep their
    /// digits after the decimal point (<c>-3.00..0.00</c>, not <c>-3..0</c>), mantissa bounds their mantissa length
    /// (<c>0.100E7</c>). <c>null</c> for a numeric type without bounds.
    /// </summary>
    private static string? FormatRange(this NumericType numericType)
    {
        if (numericType.Min is not { } min || numericType.Max is not { } max)
        {
            return null;
        }

        string Format(double value) => numericType switch
        {
            DecimalType decimalType => value.ToString("F" + Math.Max(0, -decimalType.Precision), CultureInfo.InvariantCulture),
            FloatType floatType => FormatMantissa(value, floatType.MantissaLength),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

        return $"{Format(min)}..{Format(max)}";
    }

    /// <summary>
    /// <paramref name="value"/> as <c>0.mantissa E scaling</c> with <paramref name="mantissaLength"/> mantissa digits.
    /// </summary>
    private static string FormatMantissa(double value, int mantissaLength)
    {
        // .NET writes one digit before the point (-1.00E+006), the INTERLIS mantissa starts after it (-0.100E7).
        var scientific = value.ToString("E" + (Math.Max(1, mantissaLength) - 1), CultureInfo.InvariantCulture);
        var exponentIndex = scientific.IndexOf('E');
        var sign = scientific.StartsWith('-') ? "-" : "";
        var digits = scientific[sign.Length..exponentIndex].Replace(".", "");
        var scaling = value == 0 ? 0 : int.Parse(scientific[(exponentIndex + 1)..], CultureInfo.InvariantCulture) + 1;
        return $"{sign}0.{digits}E{scaling}";
    }
}
