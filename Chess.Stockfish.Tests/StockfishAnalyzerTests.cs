// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Analysis;
using Chess.Variants.Standard;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Notation.Fen;
using Chess.Variants.Standard.Notation.Uci;
using Chess.Variants.Standard.Sides;

namespace Chess.Stockfish.Tests;

public sealed class StockfishAnalyzerTests
{
    private const string StartFen =
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public async Task StartPositionMapsSearchAndTypedMoves()
    {
        await using var host = new TestEngineHost();
        host.SetResponse(
            "info depth 12 seldepth 18 multipv 1 score cp 34 nodes 12345 " +
            "time 67 pv e2e4 e7e5 g1f3",
            "bestmove e2e4 ponder e7e5");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromInitialState(StandardInitialState.Default),
            Request(),
            TestContext.Current.CancellationToken);

        Assert.Same(SideDefinitions.White, result.Perspective);
        Assert.Equal(
            "e2e4",
            new UciMoveCodec().Format(
                Variant.CreateGame(),
                result.BestMove!.Value));
        var variation = Assert.Single(result.Variations);
        Assert.Equal(
            34,
            Assert.IsType<CentipawnScore>(variation.Score)
                .Centipawns);
        Assert.Equal(AnalysisScoreBound.Exact, variation.Bound);
        Assert.Equal(12, variation.Depth);
        Assert.Equal(18, variation.SelectiveDepth);
        Assert.Equal(12345, variation.Nodes);
        Assert.Equal(TimeSpan.FromMilliseconds(67), variation.Elapsed);
        var game = Variant.CreateGame();
        var codec = new UciMoveCodec();
        string[] tokens = ["e2e4", "e7e5", "g1f3"];
        foreach (var token in tokens)
        {
            var move = variation.PrincipalVariation[
                Array.IndexOf(tokens, token)];
            Assert.Equal(codec.Parse(game, token), move);
            game.Execute(move);
        }

        Assert.Contains($"IN position fen {StartFen}", host.ReadLogLines());
        Assert.Contains("IN go depth 10", host.ReadLogLines());
    }

    [Theory]
    [InlineData(80)]
    [InlineData(-60)]
    public async Task BlackPerspectivePreservesScoreSign(
        int score)
    {
        await using var host = new TestEngineHost();
        host.SetResponse($"info depth 4 score cp {score}", "bestmove e8e7");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen("4k3/8/8/8/8/8/4p3/4K3 b - - 0 1"),
            Request(),
            TestContext.Current.CancellationToken);
        Assert.Same(SideDefinitions.Black, result.Perspective);
        Assert.Equal(
            score,
            Assert.IsType<CentipawnScore>(
                    Assert.Single(result.Variations)
                        .Score)
                .Centipawns);
    }

    [Theory]
    [InlineData("4k3/8/8/3p4/8/8/8/4K3 w - d6 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/R3K2R w - - 87 37")]
    public async Task SendsExactCanonicalFen(
        string fen)
    {
        await using var host = new TestEngineHost();
        host.SetResponse("bestmove 0000");
        var facts = PositionFacts.FromFen(fen);
        if (fen.Contains("d6", StringComparison.Ordinal))
        {
            Assert.NotNull(facts.EnPassantTarget);
            Assert.Null(facts.EffectiveEnPassantTarget);
        }

        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        await analyzer.AnalyzeAsync(
            facts,
            Request(),
            TestContext.Current.CancellationToken);
        Assert.Contains($"IN position fen {fen}", host.ReadLogLines());
    }

    [Fact]
    public async Task HistoricalRookMovementRemovesCastlingRightsFromFen()
    {
        const string initialFen = "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1";
        const string currentFen = "r3k2r/8/8/8/8/8/8/R3K2R w Qq - 4 3";
        var initialState = new FenCodec().Parse(initialFen);
        var game = Variant.CreateGame(initialState);
        var codec = new UciMoveCodec();
        foreach (var token in new[] { "h1h2", "h8h7", "h2h1", "h7h8" })
        {
            game.Execute(codec.Parse(game, token));
        }

        var facts = PositionFacts.Evaluate(initialState, game);
        Assert.Equal(currentFen, new FenCodec().Format(facts));
        await using var host = new TestEngineHost();
        host.SetResponse("bestmove 0000");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        await analyzer.AnalyzeAsync(
            facts,
            Request(),
            TestContext.Current.CancellationToken);
        Assert.Contains($"IN position fen {currentFen}", host.ReadLogLines());
    }

    [Theory]
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "e5d6", "ep")]
    [InlineData("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1", "e1g1", "castle")]
    [InlineData("7k/P7/8/8/8/8/8/4K3 w - - 0 1", "a7a8q", "queen")]
    [InlineData("7k/P7/8/8/8/8/8/4K3 w - - 0 1", "a7a8n", "knight")]
    public async Task PreservesSpecialMoveOptions(
        string fen,
        string token,
        string kind)
    {
        await using var host = new TestEngineHost();
        host.SetResponse(
            $"info depth 1 score cp 1 pv {token}",
            $"bestmove {token}");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(fen),
            Request(),
            TestContext.Current.CancellationToken);
        var option = kind switch
        {
            "ep" => MoveOptions.EnPassant,
            "castle" => MoveOptions.CastleKingSide,
            "queen" => PromotionOptions.Queen,
            _ => PromotionOptions.Knight
        };
        Assert.Equal(
            option,
            Assert.Single(
                    Assert.Single(result.Variations)
                        .PrincipalVariation)
                .OptionId);
        Assert.Equal(option, result.BestMove!.Value.OptionId);
    }

    [Theory]
    [InlineData("0000")]
    [InlineData("(none)")]
    public async Task TerminalResultPreservesMateZero(
        string sentinel)
    {
        await using var host = new TestEngineHost();
        host.SetResponse("info depth 0 score mate 0", $"bestmove {sentinel}");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen("7k/6Q1/6K1/8/8/8/8/8 b - - 0 1"),
            Request(),
            TestContext.Current.CancellationToken);
        Assert.Same(SideDefinitions.Black, result.Perspective);
        Assert.Null(result.BestMove);
        var variation = Assert.Single(result.Variations);
        Assert.Equal(
            0,
            Assert.IsType<MateScore>(variation.Score)
                .Moves);
        Assert.Empty(variation.PrincipalVariation);
        Assert.Equal(0, variation.Depth);
    }

    [Fact]
    public async Task BestMoveWithoutScoreHasNoVariations()
    {
        await using var host = new TestEngineHost();
        host.SetResponse("info string NNUE ready", "bestmove e2e4");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            TestContext.Current.CancellationToken);
        Assert.NotNull(result.BestMove);
        Assert.Empty(result.Variations);
    }

    [Theory]
    [InlineData("info score cp 1 pv e2e5", "bestmove e2e4")]
    [InlineData("info score cp 1 pv e2e4", "bestmove e2e5")]
    public async Task InvalidMoveProducesStockfishException(
        string info,
        string bestmove)
    {
        await using var host = new TestEngineHost();
        host.SetResponse(info, bestmove);
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<StockfishException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                TestContext.Current.CancellationToken));
        Assert.IsType<FormatException>(exception.InnerException);
    }

    private static AnalysisRequest Request(
        int variationCount = 1)
    {
        return new AnalysisRequest(AnalysisLimit.ByDepth(10), variationCount);
    }
}
