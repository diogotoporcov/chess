// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Movement;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests.Rules;

public sealed class StandardHalfmoveRuleEvaluatorTests
{
    [Fact]
    public void InitialGameHasZeroClockAndNoThresholds()
    {
        var facts = Evaluate(Variant.CreateGame());

        Assert.Equal(0, facts.HalfmoveClock);
        Assert.False(facts.IsFiftyMoveThresholdReached);
        Assert.False(facts.IsSeventyFiveMoveThresholdReached);
    }

    [Fact]
    public void CommittedQuietMovesAreReflectedInFacts()
    {
        var game = Variant.CreateGame();

        TestSupport.Play(game, "g1", "f3");
        TestSupport.Play(game, "g8", "f6");

        Assert.Equal(
            2,
            Evaluate(game)
                .HalfmoveClock);
    }

    [Fact]
    public void QuietMoveAfterNinetyNineHalfmovesReachesFiftyMoveThreshold()
    {
        var game = CreateLongHistoryGame();
        AccumulateNinetyNineQuietMoves(game);
        var move = TestSupport.FindMove(game, "f6", "g8");

        Assert.True(
            CreateEvaluator(game)
                .WouldReachFiftyMoveThreshold(game.State, move));
    }

    [Fact]
    public void CastlingAfterNinetyNineHalfmovesReachesFiftyMoveThreshold()
    {
        var game = CreateLongHistoryGame(
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.Rook));
        AccumulateNinetyNineQuietMoves(game);

        Assert.Equal(
            99,
            Evaluate(game)
                .HalfmoveClock);

        var move = TestSupport.FindMove(
            game,
            "e8",
            "g8",
            MoveOptions.CastleKingSide);
        var snapshot = StandardGameSnapshot.Capture(game);

        Assert.True(
            CreateEvaluator(game)
                .WouldReachFiftyMoveThreshold(game.State, move));

        snapshot.AssertMatches(game);
    }

    [Fact]
    public void ResultingClockAboveThresholdRemainsReachedUnlessMoveResetsIt()
    {
        var game = CreateLongHistoryGame(
            TestSupport.At("a2", SideDefinitions.White, PieceDefinitions.Pawn));
        AccumulateNinetyNineQuietMoves(game);
        TestSupport.Play(game, "f6", "g8");

        Assert.True(
            Evaluate(game)
                .IsFiftyMoveThresholdReached);

        var quietMove = TestSupport.FindMove(game, "g1", "f3");
        var pawnMove = TestSupport.FindMove(game, "a2", "a3");

        Assert.True(
            CreateEvaluator(game)
                .WouldReachFiftyMoveThreshold(game.State, quietMove));

        Assert.False(
            CreateEvaluator(game)
                .WouldReachFiftyMoveThreshold(game.State, pawnMove));
    }

    [Fact]
    public void CaptureAfterNinetyNineHalfmovesDoesNotReachThreshold()
    {
        var game = CreateLongHistoryGame(
            TestSupport.At("h5", SideDefinitions.White, PieceDefinitions.Pawn));
        AccumulateNinetyNineQuietMoves(game);
        var move = TestSupport.FindMove(game, "f6", "h5");

        Assert.False(
            CreateEvaluator(game)
                .WouldReachFiftyMoveThreshold(game.State, move));
    }

    [Fact]
    public void PawnMoveAfterNinetyNineHalfmovesDoesNotReachThreshold()
    {
        var game = CreateLongHistoryGame(
            TestSupport.At("a7", SideDefinitions.Black, PieceDefinitions.Pawn));
        AccumulateNinetyNineQuietMoves(game);
        var move = TestSupport.FindMove(game, "a7", "a6");
        var snapshot = StandardGameSnapshot.Capture(game);

        Assert.False(
            CreateEvaluator(game)
                .WouldReachFiftyMoveThreshold(game.State, move));
        snapshot.AssertMatches(game);
    }

    [Fact]
    public void PromotionAfterNinetyNineHalfmovesDoesNotReachThreshold()
    {
        var game = CreateLongHistoryGame(
            TestSupport.At("a2", SideDefinitions.Black, PieceDefinitions.Pawn));
        AccumulateNinetyNineQuietMoves(game);
        var move = TestSupport.FindMove(
            game,
            "a2",
            "a1",
            PromotionOptions.Queen);

        Assert.False(
            CreateEvaluator(game)
                .WouldReachFiftyMoveThreshold(game.State, move));
    }

    [Fact]
    public void EnPassantResetsEvenAHypotheticalNinetyNineHalfmoveClock()
    {
        var game = Variant.CreateGame();
        TestSupport.Play(game, "e2", "e4");
        TestSupport.Play(game, "a7", "a6");
        TestSupport.Play(game, "e4", "e5");
        TestSupport.Play(game, "d7", "d5");
        var move = TestSupport.FindMove(
            game,
            "e5",
            "d6",
            MoveOptions.EnPassant);

        Assert.False(
            CreateEvaluator(game)
                .WouldReachFiftyMoveThreshold(game.State, move));

        var execution = game.Execute(move)
            .Execution;

        Assert.Equal(0, StandardHalfmoveRules.GetNextClock(99, execution));
    }

    [Fact]
    public void IllegalProspectiveMoveThrows()
    {
        var game = Variant.CreateGame();
        var snapshot = StandardGameSnapshot.Capture(game);
        var move = new Move(TestSupport.Square("e2"), TestSupport.Square("e5"));

        Assert.Throws<InvalidOperationException>(() =>
            Variant.DefaultHalfmoveRuleEvaluator.WouldReachFiftyMoveThreshold(
                game.State,
                move));

        snapshot.AssertMatches(game);
    }

    [Fact]
    public void ProspectiveEvaluationDelegatesNullStateValidation()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Variant.DefaultHalfmoveRuleEvaluator.WouldReachFiftyMoveThreshold(
                null!,
                default));
    }

    [Fact]
    public void ProspectiveEvaluationDoesNotMutateSourceGame()
    {
        var game = Variant.CreateGame();
        TestSupport.Play(game, "g1", "f3");
        var snapshot = StandardGameSnapshot.Capture(game);
        var move = TestSupport.FindMove(game, "g8", "f6");

        _ = CreateEvaluator(game)
            .WouldReachFiftyMoveThreshold(game.State, move);

        snapshot.AssertMatches(game);
    }

    [Fact]
    public void UndoAndReexecuteNaturallyChangeHalfmoveFacts()
    {
        var game = Variant.CreateGame();
        TestSupport.Play(game, "g1", "f3");
        var lastMove = TestSupport.Play(game, "g8", "f6")
            .Execution.Move;

        Assert.Equal(
            2,
            Evaluate(game)
                .HalfmoveClock);

        game.UndoLastMove();

        Assert.Equal(
            1,
            Evaluate(game)
                .HalfmoveClock);

        game.Execute(lastMove);

        Assert.Equal(
            2,
            Evaluate(game)
                .HalfmoveClock);
    }

    private static StandardHalfmoveRuleFacts Evaluate(
        Game game)
    {
        return CreateEvaluator(game)
            .Evaluate(game.State);
    }

    private static StandardHalfmoveRuleEvaluator CreateEvaluator(
        Game game)
    {
        return TestSupport.CreateHalfmoveRuleEvaluator(game.Variant);
    }

    private static Game CreateLongHistoryGame(
        params Placement[] additionalPlacements)
    {
        return TestSupport.CreateNonTerminatingGame(
        [
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At(
                "g1",
                SideDefinitions.White,
                PieceDefinitions.Knight),
            TestSupport.At(
                "g8",
                SideDefinitions.Black,
                PieceDefinitions.Knight),
            .. additionalPlacements
        ]);
    }

    private static void AccumulateNinetyNineQuietMoves(
        Game game)
    {
        for (var cycle = 0; cycle < 24; cycle++)
        {
            PlayQuietCycle(game);
        }

        TestSupport.Play(game, "g1", "f3");
        TestSupport.Play(game, "g8", "f6");
        TestSupport.Play(game, "f3", "g1");

        Assert.Equal(
            99,
            Evaluate(game)
                .HalfmoveClock);
    }

    private static void PlayQuietCycle(
        Game game)
    {
        TestSupport.Play(game, "g1", "f3");
        TestSupport.Play(game, "g8", "f6");
        TestSupport.Play(game, "f3", "g1");
        TestSupport.Play(game, "f6", "g8");
    }
}
