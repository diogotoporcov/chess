// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Games.Variants;
using Chess.Core.Movement;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests.Rules;

public sealed class StandardRepetitionEvaluatorTests
{
    [Fact]
    public void InitialPositionHasOneOccurrence()
    {
        var game = Variant.CreateGame();
        var facts = Evaluate(game);

        Assert.Equal(1, facts.CurrentPositionOccurrences);
        Assert.False(facts.IsThreefoldRepetition);
        Assert.False(facts.IsFivefoldRepetition);
    }

    [Fact]
    public void CurrentRepetitionEvaluationDoesNotMutateSourceGame()
    {
        var game = Variant.CreateGame();

        PlayInitialPositionCycle(game);

        var snapshot = StandardGameSnapshot.Capture(game);

        _ = Evaluate(game);

        snapshot.AssertMatches(game);
    }

    [Fact]
    public void ReversibleCyclesCountSecondThirdAndFifthOccurrences()
    {
        var game = Variant.CreateGame();

        PlayInitialPositionCycle(game);
        var secondOccurrence = Evaluate(game);

        Assert.Equal(2, secondOccurrence.CurrentPositionOccurrences);

        PlayInitialPositionCycle(game);
        var thirdOccurrence = Evaluate(game);

        Assert.Equal(3, thirdOccurrence.CurrentPositionOccurrences);
        Assert.True(thirdOccurrence.IsThreefoldRepetition);
        Assert.False(thirdOccurrence.IsFivefoldRepetition);

        PlayInitialPositionCycle(game);
        PlayInitialPositionCycle(game);
        var fifthOccurrence = Evaluate(game);

        Assert.Equal(5, fifthOccurrence.CurrentPositionOccurrences);
        Assert.True(fifthOccurrence.IsThreefoldRepetition);
        Assert.True(fifthOccurrence.IsFivefoldRepetition);
    }

    [Fact]
    public void NonConsecutiveReturnsToInitialPositionAreCounted()
    {
        var game = Variant.CreateGame();

        PlayInitialPositionCycle(game);
        TestSupport.Play(game, "b1", "c3");
        TestSupport.Play(game, "b8", "c6");
        TestSupport.Play(game, "c3", "b1");
        TestSupport.Play(game, "c6", "b8");

        Assert.Equal(
            3,
            Evaluate(game)
                .CurrentPositionOccurrences);
    }

    [Fact]
    public void ReturnedPieceLayoutWithLostCastlingRightsIsNotRepetition()
    {
        var definition = TestSupport.CreateDefinition(
            new TurnOrder(SideDefinitions.White, SideDefinitions.Black),
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("h1", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King));
        var game = definition.CreateGame();
        var evaluator = CreateEvaluator(definition);

        TestSupport.Play(game, "h1", "h2");
        TestSupport.Play(game, "e8", "e7");
        TestSupport.Play(game, "h2", "h1");
        TestSupport.Play(game, "e7", "e8");

        Assert.Equal(
            1,
            evaluator.Evaluate(game.State)
                .CurrentPositionOccurrences);
    }

    [Fact]
    public void LegalEnPassantTargetParticipatesInReconstructedPositionKey()
    {
        var game = Variant.CreateGame();

        TestSupport.Play(game, "e2", "e4");
        TestSupport.Play(game, "a7", "a6");
        TestSupport.Play(game, "e4", "e5");
        TestSupport.Play(game, "d7", "d5");

        Assert.Equal(
            1,
            Evaluate(game)
                .CurrentPositionOccurrences);
        Assert.Equal(
            TestSupport.Square("d6"),
            Variant.DefaultPositionFactsEvaluator.CreatePositionKey(game.State)
                .EffectiveEnPassantTarget);
    }

    [Fact]
    public void PinnedEnPassantDoesNotParticipateInReconstructedPositionKey()
    {
        var definition = TestSupport.CreateDefinition(
            SideDefinitions.Black,
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e5", SideDefinitions.White, PieceDefinitions.Pawn),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.Rook),
            TestSupport.At("d7", SideDefinitions.Black, PieceDefinitions.Pawn));
        var game = definition.CreateGame();
        var evaluator = CreateEvaluator(definition);

        TestSupport.Play(game, "d7", "d5");

        Assert.Equal(
            1,
            evaluator.Evaluate(game.State)
                .CurrentPositionOccurrences);
        Assert.Null(
            TestSupport
                .CreatePositionFactsEvaluator(definition)
                .CreatePositionKey(game.State)
                .EffectiveEnPassantTarget);
    }

    [Fact]
    public void ReplayPreservesPromotionMoveOption()
    {
        var definition = TestSupport.CreateDefinition(
            new TurnOrder(SideDefinitions.White, SideDefinitions.Black),
            TestSupport.At("h1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("a7", SideDefinitions.White, PieceDefinitions.Pawn));
        var game = definition.CreateGame();
        var evaluator = CreateEvaluator(definition);

        TestSupport.Play(game, "a7", "a8", PromotionOptions.Queen);

        Assert.Equal(
            1,
            evaluator.Evaluate(game.State)
                .CurrentPositionOccurrences);
        Assert.Equal(
            PieceDefinitions.Queen,
            TestSupport.PieceAt(game, "a8")
                .Definition);
        Assert.Contains(
            TestSupport
                .CreatePositionFactsEvaluator(definition)
                .CreatePositionKey(game.State)
                .PiecePlacements,
            placement => placement.Square == TestSupport.Square("a8") &&
                         placement.PieceDefinitionId ==
                         PieceDefinitions.Queen.Id.Value);
    }

    [Fact]
    public void ReconstructedStateMustMatchSuppliedState()
    {
        var definition = TestSupport.CreateDefinition(
            new TurnOrder(SideDefinitions.White, SideDefinitions.Black),
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King));
        var game = definition.CreateGame();
        var snapshot = StandardGameSnapshot.Capture(game);
        var mismatchedEvaluator = CreateEvaluator(
            TestSupport.CreateDefinition(
                new TurnOrder(SideDefinitions.White, SideDefinitions.Black),
                TestSupport.At(
                    "e1",
                    SideDefinitions.White,
                    PieceDefinitions.King),
                TestSupport.At(
                    "e8",
                    SideDefinitions.Black,
                    PieceDefinitions.King),
                TestSupport.At(
                    "g1",
                    SideDefinitions.White,
                    PieceDefinitions.Knight)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            mismatchedEvaluator.Evaluate(game.State));

        Assert.Contains("cannot be reconstructed", exception.Message);
        snapshot.AssertMatches(game);
    }

    [Fact]
    public void
        LegalMoveThatWouldCreateThirdOccurrenceReturnsTrueWithoutMutation()
    {
        var game = Variant.CreateGame();

        PlayInitialPositionCycle(game);
        TestSupport.Play(game, "g1", "f3");
        TestSupport.Play(game, "g8", "f6");
        TestSupport.Play(game, "f3", "g1");

        var snapshot = StandardGameSnapshot.Capture(game);
        var createsThird = TestSupport.FindMove(game, "f6", "g8");
        var doesNotCreateThird = TestSupport.FindMove(game, "b8", "c6");

        Assert.True(
            Variant.DefaultRepetitionEvaluator.WouldCreateThreefoldRepetition(
                game.State,
                createsThird));
        Assert.False(
            Variant.DefaultRepetitionEvaluator.WouldCreateThreefoldRepetition(
                game.State,
                doesNotCreateThird));
        snapshot.AssertMatches(game);
    }

    [Fact]
    public void ProspectiveRepetitionDistinguishesLostCastlingRights()
    {
        var initialState = TestSupport.CreateInitialState(
            SideDefinitions.White,
            [
                TestSupport.At(
                    "e1",
                    SideDefinitions.White,
                    PieceDefinitions.King),
                TestSupport.At(
                    "h1",
                    SideDefinitions.White,
                    PieceDefinitions.Rook),
                TestSupport.At(
                    "e8",
                    SideDefinitions.Black,
                    PieceDefinitions.King)
            ],
            new CastlingRights(true, false, false, false));
        var game = Variant.CreateGame(initialState);
        var evaluator = TestSupport.CreateRepetitionEvaluator(initialState);

        PlayCastlingRightsCycle(game);
        TestSupport.Play(game, "h1", "h2");
        TestSupport.Play(game, "e8", "e7");
        TestSupport.Play(game, "h2", "h1");

        var snapshot = StandardGameSnapshot.Capture(game);
        var candidate = TestSupport.FindMove(game, "e7", "e8");

        Assert.False(
            evaluator.WouldCreateThreefoldRepetition(game.State, candidate));
        snapshot.AssertMatches(game);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ProspectiveRepetitionUsesOnlyLegalEnPassantAvailability(
        bool hasLegalEnPassant,
        bool expectedThreefold)
    {
        var initialState = CreateEnPassantIdentityState(hasLegalEnPassant);
        var game = Variant.CreateGame(initialState);
        var evaluator = TestSupport.CreateRepetitionEvaluator(initialState);

        PlayKingCycle(game);
        TestSupport.Play(game, "a1", "a2");
        TestSupport.Play(game, "h8", "h7");
        TestSupport.Play(game, "a2", "a1");

        var snapshot = StandardGameSnapshot.Capture(game);
        var candidate = TestSupport.FindMove(game, "h7", "h8");

        Assert.Equal(
            expectedThreefold,
            evaluator.WouldCreateThreefoldRepetition(game.State, candidate));
        snapshot.AssertMatches(game);
    }

    [Fact]
    public void IllegalMoveThrowsWithoutMutatingSourceGame()
    {
        var game = Variant.CreateGame();
        var snapshot = StandardGameSnapshot.Capture(game);
        var illegalMove = new Move(
            TestSupport.Square("e2"),
            TestSupport.Square("e5"));

        Assert.Throws<InvalidOperationException>(() =>
            Variant.DefaultRepetitionEvaluator.WouldCreateThreefoldRepetition(
                game.State,
                illegalMove));

        snapshot.AssertMatches(game);
    }

    [Fact]
    public void ReplayUsesFreshGameStateWithoutGameOrStatus()
    {
        var turnOrder = new TurnOrder(
            SideDefinitions.White,
            SideDefinitions.Black);
        var placements = new[]
        {
            TestSupport.At(
                "e1",
                SideDefinitions.White,
                PieceDefinitions.King),
            TestSupport.At(
                "e8",
                SideDefinitions.Black,
                PieceDefinitions.King),
            TestSupport.At(
                "g1",
                SideDefinitions.White,
                PieceDefinitions.Knight),
            TestSupport.At(
                "g8",
                SideDefinitions.Black,
                PieceDefinitions.Knight)
        };
        var executionResolver = TestSupport.CreateExecutionResolver();
        var legalMoveGenerator =
            TestSupport.CreateLegalMoveGenerator(executionResolver);
        var gameStateFactory = TestSupport.CreateGameStateFactory(
            turnOrder,
            placements);
        var sourceDefinition = TestSupport.CreateDefinition(
            gameStateFactory,
            legalMoveGenerator,
            executionResolver,
            new NonTerminatingStatusEvaluator());
        var sourceGame = sourceDefinition.CreateGame();
        PlayInitialPositionCycle(sourceGame);
        var moveResolver = new GameMoveResolver(
            legalMoveGenerator,
            executionResolver);
        var evaluator = new StandardRepetitionEvaluator(
            TestSupport.CreatePositionFactsEvaluator(
                legalMoveGenerator,
                gameStateFactory),
            gameStateFactory.Create,
            new GameMoveExecutor(moveResolver));

        var facts = evaluator.Evaluate(sourceGame.State);

        Assert.Equal(2, facts.CurrentPositionOccurrences);
    }

    [Fact]
    public void RepeatedEvaluationProcessesOnlyChangedHistory()
    {
        var game = Variant.CreateGame();
        var (evaluator, replayResolver) = CreateCountingEvaluator(game.Variant);

        PlayInitialPositionCycle(game);

        _ = evaluator.Evaluate(game.State);

        Assert.Equal(4, replayResolver.ResolveCount);

        _ = evaluator.Evaluate(game.State);

        Assert.Equal(4, replayResolver.ResolveCount);

        TestSupport.Play(game, "b1", "c3");
        _ = evaluator.Evaluate(game.State);

        Assert.Equal(5, replayResolver.ResolveCount);

        game.UndoLastMove();
        _ = evaluator.Evaluate(game.State);

        Assert.Equal(5, replayResolver.ResolveCount);

        TestSupport.Play(game, "b1", "a3");
        _ = evaluator.Evaluate(game.State);

        Assert.Equal(6, replayResolver.ResolveCount);
    }

    [Fact]
    public void ProspectiveEvaluationReusesAndRestoresTrackedState()
    {
        var game = Variant.CreateGame();
        var (evaluator, replayResolver) = CreateCountingEvaluator(game.Variant);

        PlayInitialPositionCycle(game);
        TestSupport.Play(game, "g1", "f3");
        TestSupport.Play(game, "g8", "f6");
        TestSupport.Play(game, "f3", "g1");

        var snapshot = StandardGameSnapshot.Capture(game);

        _ = evaluator.Evaluate(game.State);

        Assert.Equal(7, replayResolver.ResolveCount);

        var candidate = TestSupport.FindMove(game, "f6", "g8");

        Assert.True(
            evaluator.WouldCreateThreefoldRepetition(game.State, candidate));
        Assert.Equal(8, replayResolver.ResolveCount);

        _ = evaluator.Evaluate(game.State);

        Assert.Equal(8, replayResolver.ResolveCount);
        snapshot.AssertMatches(game);
    }

    [Fact]
    public void TrackersAreIsolatedForIndependentGameStates()
    {
        var firstGame = Variant.CreateGame();
        var secondGame = Variant.CreateGame();
        var (evaluator, replayResolver) = CreateCountingEvaluator(
            firstGame.Variant);

        TestSupport.Play(firstGame, "g1", "f3");
        TestSupport.Play(secondGame, "g1", "f3");
        TestSupport.Play(secondGame, "g8", "f6");

        Assert.Equal(
            1,
            evaluator.Evaluate(firstGame.State)
                .CurrentPositionOccurrences);
        Assert.Equal(1, replayResolver.ResolveCount);

        Assert.Equal(
            1,
            evaluator.Evaluate(secondGame.State)
                .CurrentPositionOccurrences);
        Assert.Equal(3, replayResolver.ResolveCount);

        _ = evaluator.Evaluate(firstGame.State);

        Assert.Equal(3, replayResolver.ResolveCount);
    }

    [Fact]
    public void UndoAndReexecuteNaturallyChangeRepetitionFacts()
    {
        var game = Variant.CreateGame();

        PlayInitialPositionCycle(game);
        PlayInitialPositionCycle(game);
        Assert.True(
            Evaluate(game)
                .IsThreefoldRepetition);

        var lastMove = game.UndoLastMove()
            .Execution.Move;

        Assert.False(
            Evaluate(game)
                .IsThreefoldRepetition);

        game.Execute(lastMove);

        Assert.True(
            Evaluate(game)
                .IsThreefoldRepetition);
    }

    private static StandardRepetitionFacts Evaluate(
        Game game)
    {
        return Variant.DefaultRepetitionEvaluator.Evaluate(game.State);
    }

    private static StandardRepetitionEvaluator CreateEvaluator(
        GameVariantDefinition definition)
    {
        var executionResolver = TestSupport.CreateExecutionResolver();
        var legalMoveGenerator =
            TestSupport.CreateLegalMoveGenerator(executionResolver);
        var moveResolver = new GameMoveResolver(
            legalMoveGenerator,
            executionResolver);
        var gameStateFactory = TestSupport.CreateGameStateFactory(definition);

        return new StandardRepetitionEvaluator(
            TestSupport.CreatePositionFactsEvaluator(
                legalMoveGenerator,
                gameStateFactory),
            gameStateFactory.Create,
            new GameMoveExecutor(moveResolver));
    }

    private static ( StandardRepetitionEvaluator Evaluator,
        CountingMoveExecutionResolver ReplayResolver) CreateCountingEvaluator(
            GameVariantDefinition definition)
    {
        var executionResolver = TestSupport.CreateExecutionResolver();
        var legalMoveGenerator =
            TestSupport.CreateLegalMoveGenerator(executionResolver);
        var replayResolver = new CountingMoveExecutionResolver(
            executionResolver);
        var moveResolver = new GameMoveResolver(
            legalMoveGenerator,
            replayResolver);
        var gameStateFactory = TestSupport.CreateGameStateFactory(definition);
        var evaluator = new StandardRepetitionEvaluator(
            TestSupport.CreatePositionFactsEvaluator(
                legalMoveGenerator,
                gameStateFactory),
            gameStateFactory.Create,
            new GameMoveExecutor(moveResolver));

        return (evaluator, replayResolver);
    }

    private static void PlayInitialPositionCycle(
        Game game)
    {
        TestSupport.Play(game, "g1", "f3");
        TestSupport.Play(game, "g8", "f6");
        TestSupport.Play(game, "f3", "g1");
        TestSupport.Play(game, "f6", "g8");
    }

    private static void PlayCastlingRightsCycle(
        Game game)
    {
        TestSupport.Play(game, "h1", "h2");
        TestSupport.Play(game, "e8", "e7");
        TestSupport.Play(game, "h2", "h1");
        TestSupport.Play(game, "e7", "e8");
    }

    private static void PlayKingCycle(
        Game game)
    {
        TestSupport.Play(game, "a1", "a2");
        TestSupport.Play(game, "h8", "h7");
        TestSupport.Play(game, "a2", "a1");
        TestSupport.Play(game, "h7", "h8");
    }

    private static StandardInitialState CreateEnPassantIdentityState(
        bool hasLegalEnPassant)
    {
        var placements = new List<Placement>
        {
            TestSupport.At(
                "a1",
                SideDefinitions.White,
                PieceDefinitions.King),
            TestSupport.At(
                "h8",
                SideDefinitions.Black,
                PieceDefinitions.King),
            TestSupport.At(
                "d5",
                SideDefinitions.Black,
                PieceDefinitions.Pawn)
        };

        if (hasLegalEnPassant)
        {
            placements.Add(
                TestSupport.At(
                    "e5",
                    SideDefinitions.White,
                    PieceDefinitions.Pawn));
        }

        return TestSupport.CreateInitialState(
            SideDefinitions.White,
            placements,
            enPassantTarget: TestSupport.Square("d6"));
    }

    private sealed class CountingMoveExecutionResolver(
        IMoveExecutionResolver inner) : IMoveExecutionResolver
    {
        public int ResolveCount { get; private set; }

        public bool CanResolve(
            Move move)
        {
            return inner.CanResolve(move);
        }

        public MoveExecution Resolve(
            GameState gameState,
            Move move)
        {
            ResolveCount++;

            return inner.Resolve(gameState, move);
        }
    }
}
