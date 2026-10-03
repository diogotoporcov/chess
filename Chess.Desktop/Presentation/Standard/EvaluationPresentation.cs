// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Chess.Analysis;
using Chess.Core.Sides;
using Chess.Variants.Standard.Sides;

namespace Chess.Desktop.Presentation.Standard;

public sealed record EvaluationPresentation(double WhiteFraction, string Text)
{
    public static EvaluationPresentation FromResult(
        AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var variation = result.Variations.FirstOrDefault();
        if (variation is null)
        {
            return new EvaluationPresentation(0.5, "—");
        }

        return FromScore(variation.Score, result.Perspective, variation.Bound);
    }

    public static EvaluationPresentation FromScore(
        AnalysisScore score,
        Side perspective,
        AnalysisScoreBound bound = AnalysisScoreBound.Exact)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(perspective);
        var sign = perspective == SideDefinitions.White ? 1 : -1;
        if (sign < 0)
        {
            bound = bound switch
            {
                AnalysisScoreBound.Lower => AnalysisScoreBound.Upper,
                AnalysisScoreBound.Upper => AnalysisScoreBound.Lower,
                _ => bound
            };
        }

        var prefix = bound switch
        {
            AnalysisScoreBound.Exact => "",
            AnalysisScoreBound.Lower => "≥ ",
            AnalysisScoreBound.Upper => "≤ ",
            _ => throw new ArgumentOutOfRangeException(nameof(bound))
        };

        return score switch
        {
            CentipawnScore cp => new EvaluationPresentation(
                FractionForWhiteCentipawns((long)sign * cp.Centipawns),
                prefix + FormatCentipawns((long)sign * cp.Centipawns)),
            MateScore mate => CreateMatePresentation(
                mate,
                perspective,
                sign,
                prefix),
            _ => throw new ArgumentException(
                "Unknown score type.",
                nameof(score))
        };
    }

    private static EvaluationPresentation CreateMatePresentation(
        MateScore mate,
        Side perspective,
        int sign,
        string prefix)
    {
        var whiteMoves = (long)sign * mate.Moves;
        var whiteWinning = mate.Moves == 0
            ? perspective == SideDefinitions.Black
            : whiteMoves > 0;
        var text = whiteWinning
            ? $"M{Math.Abs((long)mate.Moves)}"
            : $"-M{Math.Abs((long)mate.Moves)}";
        return new EvaluationPresentation(whiteWinning ? 1 : 0, prefix + text);
    }

    public static double FractionForCentipawns(
        int whiteCentipawns) =>
        FractionForWhiteCentipawns(whiteCentipawns);

    private static double FractionForWhiteCentipawns(
        long whiteCentipawns)
    {
        var clamped = Math.Clamp(whiteCentipawns, -1000, 1000);
        return 1.0 / (1.0 + Math.Exp(-0.00368208 * clamped));
    }

    private static string FormatCentipawns(
        long whiteCentipawns)
    {
        var value = whiteCentipawns / 100.0;
        return value > 0
            ? value.ToString("+0.00", CultureInfo.InvariantCulture)
            : value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
