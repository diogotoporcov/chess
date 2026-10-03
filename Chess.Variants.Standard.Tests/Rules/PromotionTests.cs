// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Games.Status;
using Chess.Core.Movement;
using Chess.Core.Pieces;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests.Rules;

public sealed class PromotionTests
{
    [Fact]
    public void BlackCapturingUnderpromotionAndUndoRestoreExactState()
    {
        var game = TestSupport.CreateGame(
            SideDefinitions.Black,
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("a2", SideDefinitions.Black, PieceDefinitions.Pawn),
            TestSupport.At("b1", SideDefinitions.White, PieceDefinitions.Rook));
        var snapshot = StandardGameSnapshot.Capture(game);
        var pawn = TestSupport.PieceAt(game, "a2");
        var capturedRook = TestSupport.PieceAt(game, "b1");

        Assert.Same(SideDefinitions.Black, game.State.CurrentSide);
        AssertPromotionOptions(game, "a2", "a1");
        AssertPromotionOptions(game, "a2", "b1");
        Assert.Equal(
            8,
            game
                .GenerateMoves(TestSupport.Square("a2"))
                .Count());

        TestSupport.Play(game, "a2", "b1", PromotionOptions.Knight);

        var promotedKnight = TestSupport.PieceAt(game, "b1");
        Assert.Same(SideDefinitions.Black, promotedKnight.Side);
        Assert.Same(PieceDefinitions.Knight, promotedKnight.Definition);
        Assert.NotSame(pawn, promotedKnight);
        Assert.NotSame(capturedRook, promotedKnight);
        Assert.False(game.BoardState.TryGetSquare(pawn, out _));
        Assert.False(game.BoardState.TryGetSquare(capturedRook, out _));

        game.UndoLastMove();

        snapshot.AssertMatches(game);
        Assert.False(game.BoardState.TryGetSquare(promotedKnight, out _));
    }

    [Fact]
    public void PromotionGeneratesExactlyFourChoicesForMoveAndCapture()
    {
        var game = CreatePromotionGame();

        AssertPromotionOptions(game, "a7", "a8");
        AssertPromotionOptions(game, "a7", "b8");
        Assert.Equal(
            8,
            game
                .GenerateMoves(TestSupport.Square("a7"))
                .Count());
    }

    [Theory]
    [InlineData("queen")]
    [InlineData("rook")]
    [InlineData("bishop")]
    [InlineData("knight")]
    public void PromotionCreatesRequestedPieceWithPawnSide(
        string choice)
    {
        var game = CreatePromotionGame();
        var option = ResolveOption(choice);
        var definition = ResolveDefinition(choice);

        TestSupport.Play(game, "a7", "a8", option);

        var promoted = TestSupport.PieceAt(game, "a8");
        Assert.Same(SideDefinitions.White, promoted.Side);
        Assert.Same(definition, promoted.Definition);
        Assert.False(game.BoardState.IsOccupied(TestSupport.Square("a7")));
    }

    [Fact]
    public void CapturingUnderpromotionAndUndoRestorePawnAndCapturedPiece()
    {
        var game = CreatePromotionGame();
        var snapshot = StandardGameSnapshot.Capture(game);
        var captured = TestSupport.PieceAt(game, "b8");

        TestSupport.Play(game, "a7", "b8", PromotionOptions.Knight);

        var promoted = TestSupport.PieceAt(game, "b8");
        Assert.Same(PieceDefinitions.Knight, promoted.Definition);
        Assert.NotSame(captured, promoted);

        game.UndoLastMove();
        snapshot.AssertMatches(game);
    }

    [Fact]
    public void PromotionEffectCanImmediatelyCheckmate()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("f7", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At(
                "c2",
                SideDefinitions.White,
                PieceDefinitions.Bishop),
            TestSupport.At("e7", SideDefinitions.White, PieceDefinitions.Pawn),
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.King));

        Assert.Equal(StatusDefinitions.Active, game.Status.Id);

        TestSupport.Play(game, "e7", "e8", PromotionOptions.Queen);

        Assert.Equal(StatusDefinitions.Checkmate, game.Status.Id);
        Assert.Equal(
            TerminationDefinitions.Checkmate,
            Assert.IsType<GameOutcome>(game.Outcome)
                .Termination);
    }

    [Fact]
    public void KnightUnderpromotionImmediatelyUsesKnightAttacks()
    {
        var knightGame = CreateImmediateEffectGame();
        var queenGame = CreateImmediateEffectGame();

        TestSupport.Play(knightGame, "g7", "g8", PromotionOptions.Knight);
        TestSupport.Play(queenGame, "g7", "g8", PromotionOptions.Queen);

        Assert.Same(
            PieceDefinitions.Knight,
            TestSupport.PieceAt(knightGame, "g8")
                .Definition);
        Assert.Equal(StatusDefinitions.Check, knightGame.Status.Id);
        Assert.Equal(StatusDefinitions.Active, queenGame.Status.Id);
    }

    private static Game CreatePromotionGame()
    {
        return TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("a7", SideDefinitions.White, PieceDefinitions.Pawn),
            TestSupport.At("b8", SideDefinitions.Black, PieceDefinitions.Rook));
    }

    private static Game CreateImmediateEffectGame()
    {
        return TestSupport.CreateGame(
            TestSupport.At("a1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("g7", SideDefinitions.White, PieceDefinitions.Pawn),
            TestSupport.At("h6", SideDefinitions.Black, PieceDefinitions.King));
    }

    private static void AssertPromotionOptions(
        Game game,
        string from,
        string to)
    {
        var options = game
            .GenerateMoves(TestSupport.Square(from))
            .Where(move => move.To == TestSupport.Square(to))
            .Select(move => move.OptionId)
            .ToArray();

        Assert.Equal(PromotionOptions.All.Count, options.Length);
        Assert.All(
            PromotionOptions.All,
            option => Assert.Contains(option, options));
    }

    private static MoveOptionId ResolveOption(
        string choice)
    {
        return choice switch
        {
            "queen" => PromotionOptions.Queen,
            "rook" => PromotionOptions.Rook,
            "bishop" => PromotionOptions.Bishop,
            "knight" => PromotionOptions.Knight,
            _ => throw new ArgumentOutOfRangeException(nameof(choice))
        };
    }

    private static PieceDefinition ResolveDefinition(
        string choice)
    {
        return choice switch
        {
            "queen" => PieceDefinitions.Queen,
            "rook" => PieceDefinitions.Rook,
            "bishop" => PieceDefinitions.Bishop,
            "knight" => PieceDefinitions.Knight,
            _ => throw new ArgumentOutOfRangeException(nameof(choice))
        };
    }
}
