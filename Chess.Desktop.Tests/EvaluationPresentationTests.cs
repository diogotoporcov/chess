// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Analysis;
using Chess.Desktop.Presentation.Standard;
using Chess.Variants.Standard.Sides;

namespace Chess.Desktop.Tests;

public sealed class EvaluationPresentationTests
{
    [Theory]
    [InlineData(100, "+1.00")]
    [InlineData(0, "0.00")]
    [InlineData(-100, "-1.00")]
    public void WhitePerspectiveCentipawns(
        int value,
        string expected)
    {
        Assert.Equal(
            expected,
            EvaluationPresentation.FromScore(
                    new CentipawnScore(value),
                    SideDefinitions.White)
                .Text);
    }

    [Theory]
    [InlineData(100, "-1.00")]
    [InlineData(-100, "+1.00")]
    public void BlackPerspectiveFlipsScore(
        int value,
        string expected)
    {
        Assert.Equal(
            expected,
            EvaluationPresentation.FromScore(
                    new CentipawnScore(value),
                    SideDefinitions.Black)
                .Text);
    }

    [Theory]
    [InlineData(true, 3, "M3", 1.0)]
    [InlineData(true, -2, "-M2", 0.0)]
    [InlineData(false, 3, "-M3", 0.0)]
    [InlineData(false, -2, "M2", 1.0)]
    public void MateIsWhitePositive(
        bool white,
        int moves,
        string text,
        double fraction)
    {
        var result = EvaluationPresentation.FromScore(
            new MateScore(moves),
            white ? SideDefinitions.White : SideDefinitions.Black);
        Assert.Equal(text, result.Text);
        Assert.Equal(fraction, result.WhiteFraction);
    }

    [Fact]
    public void WhitePerspectiveMateZeroMeansBlackIsWinning()
    {
        var result = EvaluationPresentation.FromScore(
            new MateScore(0),
            SideDefinitions.White);

        Assert.Equal("-M0", result.Text);
        Assert.Equal(0, result.WhiteFraction);
    }

    [Fact]
    public void BlackPerspectiveMateZeroMeansWhiteIsWinning()
    {
        var result = EvaluationPresentation.FromScore(
            new MateScore(0),
            SideDefinitions.Black);

        Assert.Equal("M0", result.Text);
        Assert.Equal(1, result.WhiteFraction);
    }

    [Fact]
    public void FromResultPreservesBlackPerspectiveMateZero()
    {
        var result = new AnalysisResult(
            SideDefinitions.Black,
            null,
            new AnalysisVariation(
                new MateScore(0),
                AnalysisScoreBound.Exact,
                []));

        var presentation = EvaluationPresentation.FromResult(result);

        Assert.Equal("M0", presentation.Text);
        Assert.Equal(1, presentation.WhiteFraction);
    }

    [Fact]
    public void BoundsSurviveNormalization()
    {
        Assert.Equal(
            "+0.42",
            EvaluationPresentation.FromScore(
                    new CentipawnScore(42),
                    SideDefinitions.White)
                .Text);
        Assert.Equal(
            "≥ +0.42",
            EvaluationPresentation.FromScore(
                    new CentipawnScore(42),
                    SideDefinitions.White,
                    AnalysisScoreBound.Lower)
                .Text);
        Assert.Equal(
            "≤ +0.42",
            EvaluationPresentation.FromScore(
                    new CentipawnScore(42),
                    SideDefinitions.White,
                    AnalysisScoreBound.Upper)
                .Text);
        Assert.Equal(
            "≤ -0.42",
            EvaluationPresentation.FromScore(
                    new CentipawnScore(42),
                    SideDefinitions.Black,
                    AnalysisScoreBound.Lower)
                .Text);
    }

    [Fact]
    public void FractionIsSymmetricAndBounded()
    {
        Assert.Equal(0.5, EvaluationPresentation.FractionForCentipawns(0));
        foreach (var value in new[] { 1, 100, 1000, int.MaxValue })
        {
            var positive = EvaluationPresentation.FractionForCentipawns(value);
            var negative = EvaluationPresentation.FractionForCentipawns(-value);
            Assert.InRange(positive, 0, 1);
            Assert.InRange(negative, 0, 1);
            Assert.True(positive > 0.5);
            Assert.True(negative < 0.5);
            Assert.InRange(Math.Abs(positive + negative - 1), 0, 1e-12);
        }
    }
}
