// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Variants.Standard.Notation.Fen;
using Chess.Variants.Standard.Notation.Uci;

namespace Chess.Variants.Standard.Tests.Rules;

public sealed class PublicPositionFactsFactoryTests
{
    [Fact]
    public void StartingAndOrdinaryMoveFactsMatchFen()
    {
        var initial = Variant.DefaultInitialState;
        var game = Variant.CreateGame(initial);
        var evaluator = Variant.CreatePositionFactsEvaluator(initial);
        var fen = new FenCodec();
        Assert.Equal(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            fen.Format(evaluator.Evaluate(game.State)));
        var moves = new UciMoveCodec();
        game.Execute(moves.Parse(game, "e2e4"));
        Assert.Equal(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            fen.Format(evaluator.Evaluate(game.State)));
        game.Execute(moves.Parse(game, "g8f6"));
        Assert.Equal(
            "rnbqkb1r/pppppppp/5n2/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 1 2",
            fen.Format(evaluator.Evaluate(game.State)));
    }

    [Fact]
    public void CustomInitialStateKeepsCountersAndHistoricalCastlingLoss()
    {
        var initial = new FenCodec().Parse(
            "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 7 12");
        var game = Variant.CreateGame(initial);
        var evaluator = Variant.CreatePositionFactsEvaluator(initial);
        var moves = new UciMoveCodec();
        foreach (var token in new[] { "h1h2", "h8h7", "h2h1", "h7h8" })
        {
            game.Execute(moves.Parse(game, token));
        }

        Assert.Equal(
            "r3k2r/8/8/8/8/8/8/R3K2R w Qq - 11 14",
            new FenCodec().Format(evaluator.Evaluate(game.State)));
    }

    [Fact]
    public void RawEnPassantSurvivesWhenCaptureIsUnavailable()
    {
        var game = Variant.CreateGame();
        game.Execute(new UciMoveCodec().Parse(game, "e2e4"));
        var facts = Variant
            .CreatePositionFactsEvaluator(Variant.DefaultInitialState)
            .Evaluate(game.State);
        Assert.NotNull(facts.EnPassantTarget);
        Assert.Null(facts.EffectiveEnPassantTarget);
    }
}
