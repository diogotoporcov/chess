// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Analysis;

namespace Chess.Stockfish.Tests;

public sealed class StockfishInfoParserTests
{
    [Theory]
    [InlineData(34)]
    [InlineData(0)]
    [InlineData(-125)]
    public void ParsesCentipawnsExactly(
        int value)
    {
        var line = StockfishInfoParser.Parse(
            $"info depth 12 multipv 1 score cp {value} pv e2e4");
        Assert.Equal(
            value,
            Assert.IsType<CentipawnScore>(line!.Score)
                .Centipawns);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-2)]
    [InlineData(0)]
    public void ParsesMateInMoves(
        int value)
    {
        var line = StockfishInfoParser.Parse($"info score mate {value}");
        Assert.Equal(
            value,
            Assert.IsType<MateScore>(line!.Score)
                .Moves);
    }

    [Theory]
    [InlineData("", AnalysisScoreBound.Exact)]
    [InlineData("lowerbound", AnalysisScoreBound.Lower)]
    [InlineData("upperbound", AnalysisScoreBound.Upper)]
    public void ParsesBounds(
        string suffix,
        AnalysisScoreBound expected)
    {
        Assert.Equal(
            expected,
            StockfishInfoParser.Parse($"info score cp 20 {suffix}")!.Bound);
        Assert.Equal(
            expected,
            StockfishInfoParser.Parse($"info score mate 2 {suffix}")!.Bound);
    }

    [Fact]
    public void ParsesFullLineWithWdlAndUnknownTelemetry()
    {
        var line = StockfishInfoParser.Parse(
            "info depth 18 seldepth 27 multipv 1 score cp 42 " +
            "wdl 500 400 100 nodes 123456 nps 1000000 hashfull 87 " +
            "tbhits 0 futurefield 999 time 321 pv e2e4 e7e5 g1f3");

        Assert.Equal(18, line!.Depth);
        Assert.Equal(27, line.SelectiveDepth);
        Assert.Equal(123456, line.Nodes);
        Assert.Equal(TimeSpan.FromMilliseconds(321), line.Elapsed);
        Assert.Equal(["e2e4", "e7e5", "g1f3"], line.PvTokens);
    }

    [Theory]
    [InlineData("info string NNUE evaluation using ...")]
    [InlineData("info string Available processors: ...")]
    [InlineData("info depth 19 currmove e2e4 currmovenumber 5")]
    public void IgnoresScorelessLines(
        string line)
    {
        Assert.Null(StockfishInfoParser.Parse(line));
    }

    [Fact]
    public void InfoStringContainingRecognizedMarkersIsIgnored()
    {
        Assert.Null(
            StockfishInfoParser.Parse(
                "info string depth 20 score cp 100 pv e2e4"));
    }

    [Fact]
    public void MissingMultiPvDefaultsToRankOne()
    {
        var line = StockfishInfoParser.Parse("info depth 0 score mate 0");
        Assert.Equal(1, line!.Rank);
        Assert.Empty(line.PvTokens);
        Assert.Equal(0, line.Depth);
    }

    [Theory]
    [InlineData("info score cp nope")]
    [InlineData("info score unknown 12")]
    [InlineData("info score cp 20 lowerbound upperbound")]
    [InlineData("info score cp 20 lowerbound lowerbound")]
    [InlineData("info depth -1 score cp 1")]
    [InlineData("info depth nope score cp 1")]
    [InlineData("info seldepth -1 score cp 1")]
    [InlineData("info multipv 0 score cp 1")]
    [InlineData("info multipv nope score cp 1")]
    [InlineData("info nodes -1 score cp 1")]
    [InlineData("info nodes 9223372036854775808 score cp 1")]
    [InlineData("info time -1 score cp 1")]
    [InlineData("info time 9223372036854775807 score cp 1")]
    [InlineData("info score cp 1 pv")]
    public void RejectsMalformedRecognizedFields(
        string line)
    {
        Assert.Throws<StockfishException>(() =>
            StockfishInfoParser.Parse(line));
    }

    [Theory]
    [InlineData("depth 10 depth 20")]
    [InlineData("depth 10 seldepth 15 seldepth 16")]
    [InlineData("multipv 1 multipv 2")]
    [InlineData("nodes 10 nodes 20")]
    [InlineData("time 10 time 20")]
    [InlineData("score cp 10 score cp 20")]
    public void RejectsDuplicateRecognizedFields(
        string metadata)
    {
        Assert.Throws<StockfishException>(() =>
            StockfishInfoParser.Parse($"info {metadata} score cp 1"));
    }

    [Fact]
    public void RejectsSelectiveDepthWithoutDepth()
    {
        Assert.Throws<StockfishException>(() =>
            StockfishInfoParser.Parse("info seldepth 20 score cp 10"));
    }

    [Fact]
    public void RejectsMultiplePvMarkers()
    {
        Assert.Throws<StockfishException>(() =>
            StockfishInfoParser.Parse(
                "info depth 10 score cp 1 pv e2e4 pv e7e5"));
    }

    [Fact]
    public void TokensAfterPvRemainMoveTokens()
    {
        var line = StockfishInfoParser.Parse(
            "info depth 10 score cp 1 pv e2e4 depth score time multipv");
        Assert.Equal(
            ["e2e4", "depth", "score", "time", "multipv"],
            line!.PvTokens);
    }
}
