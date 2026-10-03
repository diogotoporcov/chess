// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Board;
using Chess.Core.Movement;

namespace Chess.Analysis.Tests;

public sealed class AnalysisVariationTests
{
    [Theory]
    [InlineData(AnalysisScoreBound.Exact)]
    [InlineData(AnalysisScoreBound.Lower)]
    [InlineData(AnalysisScoreBound.Upper)]
    public void Constructor_PreservesDefinedBound(
        AnalysisScoreBound bound)
    {
        var variation = CreateVariation(bound: bound);

        Assert.Equal(bound, variation.Bound);
    }

    [Fact]
    public void Constructor_RejectsUndefinedBound()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateVariation(bound: (AnalysisScoreBound)100));
    }

    [Fact]
    public void PrincipalVariation_IsDefensivelyCopiedAndReadOnly()
    {
        var first = Move(0, 1);
        var second = Move(1, 2);
        var replacement = Move(2, 3);
        var source = new[] { first, second };

        var variation = CreateVariation(principalVariation: source);
        source[0] = replacement;

        Assert.Equal([first, second], variation.PrincipalVariation);
        var mutableView = Assert.IsType<IList<Move>>(
            variation.PrincipalVariation,
            exactMatch: false);
        Assert.Throws<NotSupportedException>(() =>
            mutableView[0] = replacement);
    }

    [Fact]
    public void PrincipalVariation_PreservesMoveOption()
    {
        var option = new MoveOptionId("test:analysis-option");
        var move = new Move(new Square(4), new Square(5), option);

        var variation = CreateVariation(principalVariation: [move]);

        Assert.Equal(move, Assert.Single(variation.PrincipalVariation));
        Assert.Same(option, variation.PrincipalVariation[0].OptionId);
    }

    [Fact]
    public void PrincipalVariation_MayBeEmpty()
    {
        var variation = CreateVariation(principalVariation: []);

        Assert.Empty(variation.PrincipalVariation);
    }

    [Fact]
    public void SearchMetadata_MayBeOmitted()
    {
        var variation = CreateVariation();

        Assert.Null(variation.Depth);
        Assert.Null(variation.SelectiveDepth);
        Assert.Null(variation.Nodes);
        Assert.Null(variation.Elapsed);
    }

    [Fact]
    public void SearchMetadata_AcceptsAndPreservesZero()
    {
        var variation = CreateVariation(
            depth: 0,
            selectiveDepth: 0,
            nodes: 0,
            elapsed: TimeSpan.Zero);

        Assert.Equal(0, variation.Depth);
        Assert.Equal(0, variation.SelectiveDepth);
        Assert.Equal(0, variation.Nodes);
        Assert.Equal(TimeSpan.Zero, variation.Elapsed);
    }

    [Fact]
    public void Constructor_DoesNotRequireSelectiveDepthAtLeastDepth()
    {
        var variation = CreateVariation(depth: 12, selectiveDepth: 3);

        Assert.Equal(12, variation.Depth);
        Assert.Equal(3, variation.SelectiveDepth);
    }

    [Fact]
    public void Constructor_RejectsNegativeMetadata()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateVariation(depth: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateVariation(selectiveDepth: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateVariation(nodes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateVariation(elapsed: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void Constructor_RejectsNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AnalysisVariation(null!, AnalysisScoreBound.Exact, []));
        Assert.Throws<ArgumentNullException>(() => new AnalysisVariation(
            new CentipawnScore(0),
            AnalysisScoreBound.Exact,
            null!));
    }

    private static AnalysisVariation CreateVariation(
        AnalysisScoreBound bound = AnalysisScoreBound.Exact,
        IEnumerable<Move>? principalVariation = null,
        int? depth = null,
        int? selectiveDepth = null,
        long? nodes = null,
        TimeSpan? elapsed = null)
    {
        return new AnalysisVariation(
            new CentipawnScore(15),
            bound,
            principalVariation ?? [Move(0, 1)],
            depth,
            selectiveDepth,
            nodes,
            elapsed);
    }

    private static Move Move(
        int from,
        int to)
    {
        return new Move(new Square(from), new Square(to));
    }
}
