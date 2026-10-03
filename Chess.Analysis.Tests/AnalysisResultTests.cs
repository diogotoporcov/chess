// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Board;
using Chess.Core.Movement;
using Chess.Core.Sides;

namespace Chess.Analysis.Tests;

public sealed class AnalysisResultTests
{
    [Fact]
    public void Constructor_PreservesExplicitPerspective()
    {
        var firstSide = new Side("test:first");
        var secondSide = new Side("test:second");

        var first = new AnalysisResult(firstSide, null);
        var second = new AnalysisResult(secondSide, null);

        Assert.Same(firstSide, first.Perspective);
        Assert.Same(secondSide, second.Perspective);
    }

    [Fact]
    public void Variations_AreDefensivelyCopiedAndReadOnly()
    {
        var first = Variation(10, Move(0, 1));
        var second = Variation(20, Move(1, 2));
        var source = new[] { first };

        var result = new AnalysisResult(new Side("test:side"), null, source);
        source[0] = second;

        Assert.Same(first, Assert.Single(result.Variations));
        var mutableView = Assert.IsType<IList<AnalysisVariation>>(
            result.Variations,
            exactMatch: false);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = second);
    }

    [Fact]
    public void Constructor_AllowsNoMoveAndNoVariations()
    {
        var result = new AnalysisResult(new Side("test:terminal"), null);

        Assert.Null(result.BestMove);
        Assert.Empty(result.Variations);
    }

    [Fact]
    public void Constructor_AllowsBestMoveWithoutVariation()
    {
        var bestMove = Move(0, 1);

        var result = new AnalysisResult(new Side("test:side"), bestMove);

        Assert.Equal(bestMove, result.BestMove);
        Assert.Empty(result.Variations);
    }

    [Fact]
    public void Constructor_DoesNotDeriveOrReconcileBestMove()
    {
        var bestMove = Move(0, 1);
        var principalVariationMove = Move(2, 3);
        var variation = Variation(10, principalVariationMove);

        var result = new AnalysisResult(
            new Side("test:side"),
            bestMove,
            variation);

        Assert.Equal(bestMove, result.BestMove);
        Assert.Equal(
            principalVariationMove,
            result
                .Variations[0]
                .PrincipalVariation[0]);
    }

    [Fact]
    public void Constructor_PreservesVariationOrder()
    {
        var first = Variation(30, Move(0, 1));
        var second = Variation(20, Move(1, 2));
        var third = Variation(10, Move(2, 3));

        var result = new AnalysisResult(
            new Side("test:side"),
            null,
            first,
            second,
            third);

        Assert.Equal([first, second, third], result.Variations);
    }

    [Fact]
    public void Constructor_RejectsNullInputs()
    {
        var side = new Side("test:side");

        Assert.Throws<ArgumentNullException>(() =>
            new AnalysisResult(null!, null));
        Assert.Throws<ArgumentNullException>(() =>
            new AnalysisResult(side, null, variations: null!));
        Assert.Throws<ArgumentException>(() =>
            new AnalysisResult(side, null, (AnalysisVariation)null!));
    }

    private static AnalysisVariation Variation(
        int centipawns,
        Move move)
    {
        return new AnalysisVariation(
            new CentipawnScore(centipawns),
            AnalysisScoreBound.Exact,
            [move]);
    }

    private static Move Move(
        int from,
        int to)
    {
        return new Move(new Square(from), new Square(to));
    }
}
