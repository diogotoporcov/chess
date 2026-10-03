// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Games.Attacks;
using Chess.Core.Movement;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests.Rules;

public sealed class CastlingTests
{
    [Theory]
    [InlineData("g1", "h1", "f1", "kingside")]
    [InlineData("c1", "a1", "d1", "queenside")]
    public void WhiteCanCastleOnEitherOpenSideAndUndoRestoresExactState(
        string kingTo,
        string rookFrom,
        string rookTo,
        string side)
    {
        var game = CreateWhiteCastlingGame();
        var snapshot = StandardGameSnapshot.Capture(game);
        var king = TestSupport.PieceAt(game, "e1");
        var rook = TestSupport.PieceAt(game, rookFrom);
        var option = side == "kingside"
            ? MoveOptions.CastleKingSide
            : MoveOptions.CastleQueenSide;

        var record = TestSupport.Play(game, "e1", kingTo, option);

        Assert.Same(king, TestSupport.PieceAt(game, kingTo));
        Assert.Same(rook, TestSupport.PieceAt(game, rookTo));
        Assert.Equal(4, record.Execution.Transition.Changes.Count);
        Assert.Same(SideDefinitions.Black, game.State.CurrentSide);

        game.UndoLastMove();
        snapshot.AssertMatches(game);
    }

    [Fact]
    public void BlackCanCastleQueenSide()
    {
        var game = TestSupport.CreateGame(
            SideDefinitions.Black,
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.Rook));

        var move = TestSupport.FindMove(
            game,
            "e8",
            "c8",
            MoveOptions.CastleQueenSide);

        game.Execute(move);

        Assert.Same(
            PieceDefinitions.King,
            TestSupport.PieceAt(game, "c8")
                .Definition);
        Assert.Same(
            PieceDefinitions.Rook,
            TestSupport.PieceAt(game, "d8")
                .Definition);
    }

    [Theory]
    [InlineData("f1", "g1", "kingside")]
    [InlineData("b1", "c1", "queenside")]
    [InlineData("d1", "c1", "queenside")]
    public void PieceBetweenKingAndRookBlocksCastling(
        string blockerSquare,
        string kingTo,
        string side)
    {
        var option = side == "kingside"
            ? MoveOptions.CastleKingSide
            : MoveOptions.CastleQueenSide;
        var rookSquare = side == "kingside" ? "h1" : "a1";
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At(
                rookSquare,
                SideDefinitions.White,
                PieceDefinitions.Rook),
            TestSupport.At(
                blockerSquare,
                SideDefinitions.White,
                PieceDefinitions.Knight),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King));

        Assert.DoesNotContain(
            new Move(
                TestSupport.Square("e1"),
                TestSupport.Square(kingTo),
                option),
            game.GenerateMoves(TestSupport.Square("e1")));
    }

    [Theory]
    [InlineData("king")]
    [InlineData("rook")]
    public void CastlingRemainsUnavailableAfterKingOrRookReturnsHome(
        string movedPiece)
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("h1", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King));

        if (movedPiece == "king")
        {
            TestSupport.Play(game, "e1", "f1");
            TestSupport.Play(game, "e8", "e7");
            TestSupport.Play(game, "f1", "e1");
        }
        else
        {
            TestSupport.Play(game, "h1", "h2");
            TestSupport.Play(game, "e8", "e7");
            TestSupport.Play(game, "h2", "h1");
        }

        TestSupport.Play(game, "e7", "e8");

        Assert.DoesNotContain(
            new Move(
                TestSupport.Square("e1"),
                TestSupport.Square("g1"),
                MoveOptions.CastleKingSide),
            game.GenerateMoves(TestSupport.Square("e1")));
    }

    [Theory]
    [InlineData("e8")]
    [InlineData("f8")]
    [InlineData("g8")]
    public void AttackOnKingPathPreventsCastling(
        string attackingRookSquare)
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("h1", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At(
                attackingRookSquare,
                SideDefinitions.Black,
                PieceDefinitions.Rook));

        Assert.DoesNotContain(
            new Move(
                TestSupport.Square("e1"),
                TestSupport.Square("g1"),
                MoveOptions.CastleKingSide),
            game.GenerateMoves(TestSupport.Square("e1")));
    }

    [Fact]
    public void PinnedAttackerOnTransitSquarePreventsCastling()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("h1", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At(
                "h2",
                SideDefinitions.Black,
                PieceDefinitions.Knight));
        var attackGenerator = new PatternAttackGenerator();

        Assert.True(
            attackGenerator.IsSquareAttacked(
                game.State,
                TestSupport.Square("f1"),
                SideDefinitions.Black));
        Assert.DoesNotContain(
            new Move(
                TestSupport.Square("e1"),
                TestSupport.Square("g1"),
                MoveOptions.CastleKingSide),
            game.GenerateMoves(TestSupport.Square("e1")));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("opponent")]
    [InlineData("wrong piece")]
    public void CastlingRequiresAnUnmovedFriendlyRookOnItsHomeSquare(
        string rookState)
    {
        var placements = new List<Placement>
        {
            TestSupport.At(
                "e1",
                SideDefinitions.White,
                PieceDefinitions.King),
            TestSupport.At(
                "e8",
                SideDefinitions.Black,
                PieceDefinitions.King)
        };

        if (rookState == "opponent")
        {
            placements.Add(
                TestSupport.At(
                    "h1",
                    SideDefinitions.Black,
                    PieceDefinitions.Rook));
        }
        else if (rookState == "wrong piece")
        {
            placements.Add(
                TestSupport.At(
                    "h1",
                    SideDefinitions.White,
                    PieceDefinitions.Bishop));
        }

        var game = TestSupport.CreateGame(placements.ToArray());

        Assert.DoesNotContain(
            new Move(
                TestSupport.Square("e1"),
                TestSupport.Square("g1"),
                MoveOptions.CastleKingSide),
            game.GenerateMoves(TestSupport.Square("e1")));
    }

    private static Game CreateWhiteCastlingGame()
    {
        return TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("a1", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("h1", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King));
    }
}
