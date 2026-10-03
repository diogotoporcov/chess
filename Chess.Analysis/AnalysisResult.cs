// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Movement;
using Chess.Core.Sides;

namespace Chess.Analysis;

public sealed record AnalysisResult
{
    public Side Perspective { get; }

    public Move? BestMove { get; }

    public IReadOnlyList<AnalysisVariation> Variations { get; }

    public AnalysisResult(
        Side perspective,
        Move? bestMove,
        params AnalysisVariation[] variations)
    {
        ArgumentNullException.ThrowIfNull(perspective);
        ArgumentNullException.ThrowIfNull(variations);

        if (Array.IndexOf(variations, null!) >= 0)
        {
            throw new ArgumentException(
                "Variations cannot contain null elements.",
                nameof(variations));
        }

        Perspective = perspective;
        BestMove = bestMove;
        Variations = Array.AsReadOnly<AnalysisVariation>([.. variations]);
    }
}
