// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Movement;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Notation.Fen;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests.Rules;

public sealed class EnPassantTests
{
    [Fact]
    public void ImmediateEnPassant_RemovesCorrectPawnAndUndoRestoresEverything()
    {
        var game = CreateImmediateOpportunity();
        var snapshot = StandardGameSnapshot.Capture(game);
        var whitePawn = TestSupport.PieceAt(game, "e5");
        var blackPawn = TestSupport.PieceAt(game, "d5");

        TestSupport.Play(game, "e5", "d6", MoveOptions.EnPassant);

        Assert.Same(whitePawn, TestSupport.PieceAt(game, "d6"));
        Assert.False(game.BoardState.IsOccupied(TestSupport.Square("d5")));
        Assert.False(game.BoardState.TryGetSquare(blackPawn, out _));

        game.UndoLastMove();
        snapshot.AssertMatches(game);
    }

    [Fact]
    public void EnPassantOpportunityExpiresAfterOneReply()
    {
        var game = CreateImmediateOpportunity();

        TestSupport.Play(game, "h2", "h3");
        TestSupport.Play(game, "a6", "a5");

        Assert.DoesNotContain(
            new Move(
                TestSupport.Square("e5"),
                TestSupport.Square("d6"),
                MoveOptions.EnPassant),
            game.GenerateMoves(TestSupport.Square("e5")));
    }

    [Fact]
    public void AdjacentPawnWithoutRequiredLastMoveDoesNotAllowEnPassant()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("e5", SideDefinitions.White, PieceDefinitions.Pawn),
            TestSupport.At("d5", SideDefinitions.Black, PieceDefinitions.Pawn));

        Assert.DoesNotContain(
            new Move(
                TestSupport.Square("e5"),
                TestSupport.Square("d6"),
                MoveOptions.EnPassant),
            game.GenerateMoves(TestSupport.Square("e5")));
    }

    [Fact]
    public void EnPassantThatWouldExposeOwnKingIsIllegal()
    {
        var game = TestSupport.CreateGame(
            SideDefinitions.Black,
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e5", SideDefinitions.White, PieceDefinitions.Pawn),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.Rook),
            TestSupport.At("d7", SideDefinitions.Black, PieceDefinitions.Pawn));

        TestSupport.Play(game, "d7", "d5");

        Assert.DoesNotContain(
            new Move(
                TestSupport.Square("e5"),
                TestSupport.Square("d6"),
                MoveOptions.EnPassant),
            game.GenerateMoves(TestSupport.Square("e5")));
    }

    [Fact]
    public void EnPassantCanRemoveTheCheckingPawn()
    {
        var initialState = new FenCodec().Parse(
            "k7/8/8/3pP3/4K3/8/8/8 w - d6 0 1");
        var game = Variant.CreateGame(initialState);
        var move = new Move(
            TestSupport.Square("e5"),
            TestSupport.Square("d6"),
            MoveOptions.EnPassant);

        Assert.Equal(StatusDefinitions.Check, game.Status.Id);
        Assert.Contains(move, game.GenerateMoves(TestSupport.Square("e5")));

        game.Execute(move);

        Assert.False(game.BoardState.IsOccupied(TestSupport.Square("d5")));
        Assert.Equal(StatusDefinitions.Active, game.Status.Id);
    }

    [Fact]
    public void BlackCanCaptureEnPassantInItsRelativeForwardDirection()
    {
        var game = Variant.CreateGame();
        TestSupport.Play(game, "a2", "a3");
        TestSupport.Play(game, "e7", "e5");
        TestSupport.Play(game, "a3", "a4");
        TestSupport.Play(game, "e5", "e4");
        TestSupport.Play(game, "d2", "d4");
        var snapshot = StandardGameSnapshot.Capture(game);

        TestSupport.Play(game, "e4", "d3", MoveOptions.EnPassant);

        Assert.Same(
            SideDefinitions.Black,
            TestSupport.PieceAt(game, "d3")
                .Side);
        Assert.False(game.BoardState.IsOccupied(TestSupport.Square("d4")));

        game.UndoLastMove();
        snapshot.AssertMatches(game);
    }

    private static Game CreateImmediateOpportunity()
    {
        var game = Variant.CreateGame();
        TestSupport.Play(game, "e2", "e4");
        TestSupport.Play(game, "a7", "a6");
        TestSupport.Play(game, "e4", "e5");
        TestSupport.Play(game, "d7", "d5");
        return game;
    }
}
