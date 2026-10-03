// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Analysis.Tests;

public sealed class AnalysisScoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(-125)]
    public void CentipawnScore_PreservesExactValue(
        int centipawns)
    {
        var score = new CentipawnScore(centipawns);

        Assert.Equal(centipawns, score.Centipawns);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-3)]
    [InlineData(0)]
    public void MateScore_PreservesExactValue(
        int moves)
    {
        var score = new MateScore(moves);

        Assert.Equal(moves, score.Moves);
    }

    [Fact]
    public void ScoreKinds_RemainStructurallyDistinct()
    {
        AnalysisScore centipawns = new CentipawnScore(10);
        AnalysisScore mate = new MateScore(2);

        Assert.IsType<CentipawnScore>(centipawns);
        Assert.IsType<MateScore>(mate);
        Assert.NotEqual(centipawns.GetType(), mate.GetType());
    }
}
