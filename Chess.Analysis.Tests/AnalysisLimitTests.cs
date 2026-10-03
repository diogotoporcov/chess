// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Analysis.Tests;

public sealed class AnalysisLimitTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void ByDepth_PreservesPositiveDepth(
        int depth)
    {
        var limit = AnalysisLimit.ByDepth(depth);

        Assert.Equal(depth, limit.Depth);
        Assert.Null(limit.Nodes);
        Assert.Null(limit.Time);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ByDepth_RejectsNonPositiveDepth(
        int depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AnalysisLimit.ByDepth(depth));
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(1_000_000L)]
    public void ByNodes_PreservesPositiveNodeCount(
        long nodes)
    {
        var limit = AnalysisLimit.ByNodes(nodes);

        Assert.Null(limit.Depth);
        Assert.Equal(nodes, limit.Nodes);
        Assert.Null(limit.Time);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ByNodes_RejectsNonPositiveNodeCount(
        long nodes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AnalysisLimit.ByNodes(nodes));
    }

    [Fact]
    public void ByTime_PreservesPositiveElapsedTime()
    {
        var millisecond = TimeSpan.FromMilliseconds(1);
        var seconds = TimeSpan.FromSeconds(5);

        var first = AnalysisLimit.ByTime(millisecond);
        var second = AnalysisLimit.ByTime(seconds);

        Assert.Null(first.Depth);
        Assert.Null(first.Nodes);
        Assert.Equal(millisecond, first.Time);
        Assert.Equal(seconds, second.Time);
    }

    [Fact]
    public void ByTime_RejectsNonPositiveElapsedTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AnalysisLimit.ByTime(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AnalysisLimit.ByTime(TimeSpan.FromTicks(-1)));
    }
}
