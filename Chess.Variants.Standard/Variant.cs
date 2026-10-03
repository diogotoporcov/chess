// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Games.Attacks;
using Chess.Core.Games.Variants;
using Chess.Core.Movement;
using Chess.Variants.Standard.Board.Regions;
using Chess.Variants.Standard.Board.Topology;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Games.Rules;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Movement.Orientation;

namespace Chess.Variants.Standard;

public static class Variant
{
    private static readonly VariantComponents Components =
        CreateComponents(StandardInitialState.Default);

    public static StandardInitialState DefaultInitialState =>
        StandardInitialState.Default;

    public static GameVariantDefinition Definition =>
        Components.VariantDefinition;

    internal static StandardPositionFactsEvaluator
        DefaultPositionFactsEvaluator =>
        Components.FactsEvaluator;

    internal static StandardRepetitionEvaluator DefaultRepetitionEvaluator =>
        Components.Repetition;

    internal static StandardHalfmoveRuleEvaluator
        DefaultHalfmoveRuleEvaluator =>
        Components.HalfmoveRules;

    public static Game CreateGame()
    {
        return Definition.CreateGame();
    }

    public static Game CreateGame(
        StandardInitialState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);

        return CreateComponents(initialState)
            .VariantDefinition
            .CreateGame();
    }

    public static StandardPositionFactsEvaluator CreatePositionFactsEvaluator(
        StandardInitialState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);

        return CreateComponents(initialState)
            .FactsEvaluator;
    }

    private static VariantComponents CreateComponents(
        StandardInitialState initialState)
    {
        var gameStateFactory = new GameStateFactory(
            BoardTopologyFactory.Create(),
            TurnOrderDefinition.Instance,
            initialState.SideToMove,
            Orientations.Resolver,
            BoardRegions.Resolver,
            [.. initialState.Placements]);

        var castlingRightsEvaluator = new CastlingRightsEvaluator(
            initialState.CastlingRights);
        var enPassantTargetEvaluator =
            new StandardEnPassantTargetEvaluator(initialState.EnPassantTarget);

        var executionResolver = new CompositeMoveExecutionResolver(
            new BasicMoveExecutionResolver(),
            new PromotionMoveExecutionResolver(),
            new EnPassantMoveExecutionResolver(enPassantTargetEvaluator),
            new CastlingMoveExecutionResolver(castlingRightsEvaluator));

        var moveSimulator = new GameMoveSimulator(executionResolver);

        var attackGenerator = new PatternAttackGenerator();

        var checkDetector = new CheckDetector(attackGenerator);

        var pseudoLegalMoveGenerator = new CastlingMoveGenerator(
            new EnPassantMoveGenerator(
                new PromotionMoveGenerator(new PseudoLegalGameMoveGenerator()),
                enPassantTargetEvaluator),
            moveSimulator,
            checkDetector,
            castlingRightsEvaluator);

        var legalMoveGenerator = new LegalMoveGenerator(
            pseudoLegalMoveGenerator,
            moveSimulator,
            checkDetector);

        var moveResolver = new GameMoveResolver(
            legalMoveGenerator,
            executionResolver);

        var moveExecutor = new GameMoveExecutor(moveResolver);

        var factsEvaluator = new StandardPositionFactsEvaluator(
            legalMoveGenerator,
            castlingRightsEvaluator,
            enPassantTargetEvaluator,
            initialState.HalfmoveClock,
            initialState.FullmoveNumber);

        var repetitionEvaluator = new StandardRepetitionEvaluator(
            factsEvaluator,
            gameStateFactory.Create,
            moveExecutor);

        var halfmoveRuleEvaluator = new StandardHalfmoveRuleEvaluator(
            factsEvaluator,
            moveResolver);

        var statusEvaluator = new StatusEvaluator(
            legalMoveGenerator,
            checkDetector,
            repetitionEvaluator,
            halfmoveRuleEvaluator);

        var definition = new GameVariantDefinition(
            new GameVariantId("chess:standard"),
            "Standard Chess",
            gameStateFactory,
            legalMoveGenerator,
            executionResolver,
            statusEvaluator);

        return new VariantComponents(
            definition,
            factsEvaluator,
            repetitionEvaluator,
            halfmoveRuleEvaluator);
    }

    private sealed record VariantComponents(
        GameVariantDefinition VariantDefinition,
        StandardPositionFactsEvaluator FactsEvaluator,
        StandardRepetitionEvaluator Repetition,
        StandardHalfmoveRuleEvaluator HalfmoveRules);
}
