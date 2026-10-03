// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Games.Attacks;
using Chess.Core.Movement;
using Chess.Variants.Standard;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Games.Rules;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Notation.Fen;

namespace Chess.Stockfish.Tests;

internal static class PositionFacts
{
    public static StandardPositionFacts FromFen(
        string fen)
    {
        return FromInitialState(new FenCodec().Parse(fen));
    }

    public static StandardPositionFacts FromInitialState(
        StandardInitialState state)
    {
        return Evaluate(state, Variant.CreateGame(state));
    }

    public static StandardPositionFacts Evaluate(
        StandardInitialState state,
        Game game)
    {
        var castling = new CastlingRightsEvaluator(state.CastlingRights);
        var enPassant = new StandardEnPassantTargetEvaluator(
            state.EnPassantTarget);
        var resolver = new CompositeMoveExecutionResolver(
            new BasicMoveExecutionResolver(),
            new PromotionMoveExecutionResolver(),
            new EnPassantMoveExecutionResolver(enPassant),
            new CastlingMoveExecutionResolver(castling));
        var simulator = new GameMoveSimulator(resolver);
        var check = new CheckDetector(new PatternAttackGenerator());
        var generator = new LegalMoveGenerator(
            new CastlingMoveGenerator(
                new EnPassantMoveGenerator(
                    new PromotionMoveGenerator(
                        new PseudoLegalGameMoveGenerator()),
                    enPassant),
                simulator,
                check,
                castling),
            simulator,
            check);
        var evaluator = new StandardPositionFactsEvaluator(
            generator,
            castling,
            enPassant,
            state.HalfmoveClock,
            state.FullmoveNumber);
        return evaluator.Evaluate(game.State);
    }
}
