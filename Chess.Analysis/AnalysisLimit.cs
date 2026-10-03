// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Analysis;

public sealed record AnalysisLimit
{
    public int? Depth { get; }

    public long? Nodes { get; }

    public TimeSpan? Time { get; }

    private AnalysisLimit(
        int? depth,
        long? nodes,
        TimeSpan? time)
    {
        Depth = depth;
        Nodes = nodes;
        Time = time;
    }

    public static AnalysisLimit ByDepth(
        int depth)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depth),
                "Depth must be greater than zero.");
        }

        return new AnalysisLimit(depth, null, null);
    }

    public static AnalysisLimit ByNodes(
        long nodes)
    {
        if (nodes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodes),
                "Nodes must be greater than zero.");
        }

        return new AnalysisLimit(null, nodes, null);
    }

    public static AnalysisLimit ByTime(
        TimeSpan time)
    {
        if (time <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(time),
                "Time must be greater than zero.");
        }

        return new AnalysisLimit(null, null, time);
    }
}
