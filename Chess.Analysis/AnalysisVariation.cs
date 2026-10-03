// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Movement;

namespace Chess.Analysis;

public sealed record AnalysisVariation
{
    public AnalysisScore Score { get; }

    public AnalysisScoreBound Bound { get; }

    public IReadOnlyList<Move> PrincipalVariation { get; }

    public int? Depth { get; }

    public int? SelectiveDepth { get; }

    public long? Nodes { get; }

    public TimeSpan? Elapsed { get; }

    public AnalysisVariation(
        AnalysisScore score,
        AnalysisScoreBound bound,
        IEnumerable<Move> principalVariation,
        int? depth = null,
        int? selectiveDepth = null,
        long? nodes = null,
        TimeSpan? elapsed = null)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(principalVariation);

        if (!Enum.IsDefined(bound))
        {
            throw new ArgumentOutOfRangeException(nameof(bound));
        }

        if (depth is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }

        if (selectiveDepth is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectiveDepth));
        }

        if (nodes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodes));
        }

        if (elapsed is { } elapsedValue &&
            elapsedValue < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        Score = score;
        Bound = bound;
        PrincipalVariation = Array.AsReadOnly([.. principalVariation]);
        Depth = depth;
        SelectiveDepth = selectiveDepth;
        Nodes = nodes;
        Elapsed = elapsed;
    }
}
