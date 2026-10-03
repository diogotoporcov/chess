// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Uci.Tests;

public sealed class UciGoCommandTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void ByDepth_AcceptsPositiveDepth(
        int depth)
    {
        Assert.NotNull(UciGoCommand.ByDepth(depth));
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(1_000_000L)]
    public void ByNodes_AcceptsPositiveNodeCount(
        long nodes)
    {
        Assert.NotNull(UciGoCommand.ByNodes(nodes));
    }

    [Fact]
    public void ByMoveTime_AcceptsRepresentablePositiveTime()
    {
        Assert.NotNull(UciGoCommand.ByMoveTime(TimeSpan.FromMilliseconds(1)));
        Assert.NotNull(UciGoCommand.ByMoveTime(TimeSpan.FromSeconds(5)));
        Assert.NotNull(UciGoCommand.ByMoveTime(TimeSpan.FromTicks(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ByDepth_RejectsNonPositiveDepth(
        int depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UciGoCommand.ByDepth(depth));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ByNodes_RejectsNonPositiveNodeCount(
        long nodes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UciGoCommand.ByNodes(nodes));
    }

    [Fact]
    public void ByMoveTime_RejectsNonPositiveTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UciGoCommand.ByMoveTime(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UciGoCommand.ByMoveTime(TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void ByMoveTime_RejectsUnrepresentableTime()
    {
        var time = TimeSpan.FromMilliseconds(int.MaxValue + 1L);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UciGoCommand.ByMoveTime(time));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UciGoCommand.ByMoveTime(TimeSpan.MaxValue));
    }
}
