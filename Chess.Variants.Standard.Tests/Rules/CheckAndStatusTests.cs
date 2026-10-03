// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Games.Attacks;
using Chess.Core.Games.Status;
using Chess.Core.Movement;
using Chess.Core.Pieces;
using Chess.Core.Sides;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Games.Rules;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests.Rules;

public sealed class CheckAndStatusTests
{
    [Theory]
    [InlineData("rook", "d8")]
    [InlineData("bishop", "a7")]
    [InlineData("queen", "d8")]
    [InlineData("knight", "c6")]
    [InlineData("pawn", "c5")]
    [InlineData("king", "e5")]
    public void CheckIsDetectedFromEveryRelevantPieceType(
        string attackerName,
        string attackerSquare)
    {
        var attacker = ResolveDefinition(attackerName);
        var placements = new List<Placement>
        {
            TestSupport.At(
                "d4",
                SideDefinitions.White,
                PieceDefinitions.King),
            TestSupport.At("h2", SideDefinitions.White, PieceDefinitions.Pawn),
            TestSupport.At(attackerSquare, SideDefinitions.Black, attacker)
        };

        if (attacker != PieceDefinitions.King)
        {
            placements.Add(
                TestSupport.At(
                    "h8",
                    SideDefinitions.Black,
                    PieceDefinitions.King));
        }

        var game = TestSupport.CreateGame(placements.ToArray());

        Assert.Equal(StatusDefinitions.Check, game.Status.Id);
        Assert.False(game.Status.IsTerminal);
        Assert.Null(game.Status.Outcome);
    }

    [Fact]
    public void PinnedPieceCannotExposeItsKing()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e2", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.Rook));
        var moves = game
            .GenerateMoves(TestSupport.Square("e2"))
            .ToArray();

        Assert.Contains(Move("e2", "e3"), moves);
        Assert.DoesNotContain(Move("e2", "d2"), moves);
        Assert.DoesNotContain(Move("e2", "f2"), moves);
    }

    [Fact]
    public void PinnedPieceStillAttacksAndRestrictsTheOpposingKing()
    {
        Placement[] placements =
        [
            TestSupport.At(
                "c4",
                SideDefinitions.White,
                PieceDefinitions.King),
            TestSupport.At(
                "e1",
                SideDefinitions.White,
                PieceDefinitions.Rook),
            TestSupport.At(
                "e8",
                SideDefinitions.Black,
                PieceDefinitions.King),
            TestSupport.At(
                "e7",
                SideDefinitions.Black,
                PieceDefinitions.Knight)
        ];
        var pinnedSideToMove = TestSupport.CreateGame(
            SideDefinitions.Black,
            placements);
        var opposingKingToMove = TestSupport.CreateGame(placements);
        var attackGenerator = new PatternAttackGenerator();

        Assert.DoesNotContain(
            Move("e7", "d5"),
            pinnedSideToMove.GenerateMoves(TestSupport.Square("e7")));
        Assert.True(
            attackGenerator.IsSquareAttacked(
                opposingKingToMove.State,
                TestSupport.Square("d5"),
                SideDefinitions.Black));
        Assert.DoesNotContain(
            Move("c4", "d5"),
            opposingKingToMove.GenerateMoves(TestSupport.Square("c4")));
    }

    [Fact]
    public void KingsCannotMoveAdjacentAndStillAttackAdjacentSquares()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("c3", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e3", SideDefinitions.Black, PieceDefinitions.King));
        var attackGenerator = new PatternAttackGenerator();

        Assert.True(
            attackGenerator.IsSquareAttacked(
                game.State,
                TestSupport.Square("d3"),
                SideDefinitions.Black));
        Assert.DoesNotContain(
            Move("c3", "d3"),
            game.GenerateMoves(TestSupport.Square("c3")));
    }

    [Fact]
    public void KingCannotMoveIntoAnAttackedSquare()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("d8", SideDefinitions.Black, PieceDefinitions.Rook));

        Assert.DoesNotContain(
            Move("e1", "d1"),
            game.GenerateMoves(TestSupport.Square("e1")));
    }

    [Fact]
    public void KingCannotCaptureOntoASquareDefendedByAnotherPiece()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e4", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At(
                "d5",
                SideDefinitions.Black,
                PieceDefinitions.Knight),
            TestSupport.At("d8", SideDefinitions.Black, PieceDefinitions.Rook));

        Assert.DoesNotContain(
            Move("e4", "d5"),
            game.GenerateMoves(TestSupport.Square("e4")));
    }

    [Fact]
    public void BlockingACheckingRayIsALegalEvasion()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At(
                "f1",
                SideDefinitions.White,
                PieceDefinitions.Bishop),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.Rook));

        Assert.Equal(StatusDefinitions.Check, game.Status.Id);
        Assert.Contains(
            Move("f1", "e2"),
            game.GenerateMoves(TestSupport.Square("f1")));
    }

    [Fact]
    public void NonKingPieceCanCaptureTheOnlyChecker()
    {
        var game = CreateKnightCheckGame(hasSecondCheckingLine: false);

        Assert.Equal(StatusDefinitions.Check, game.Status.Id);
        Assert.Contains(
            Move("g2", "f3"),
            game.GenerateMoves(TestSupport.Square("g2")));
    }

    [Fact]
    public void CapturingOneCheckerIsIllegalWhenAnotherLineRemains()
    {
        var game = CreateKnightCheckGame(hasSecondCheckingLine: true);

        Assert.Equal(StatusDefinitions.Check, game.Status.Id);
        Assert.DoesNotContain(
            Move("g2", "f3"),
            game.GenerateMoves(TestSupport.Square("g2")));
    }

    [Fact]
    public void DoubleCheckAllowsOnlyKingMoves()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At(
                "g1",
                SideDefinitions.White,
                PieceDefinitions.Knight),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.Rook),
            TestSupport.At(
                "b4",
                SideDefinitions.Black,
                PieceDefinitions.Bishop));
        var moves = TestSupport.AllMoves(game);

        Assert.Equal(StatusDefinitions.Check, game.Status.Id);
        Assert.NotEmpty(moves);
        Assert.Empty(game.GenerateMoves(TestSupport.Square("g1")));
        Assert.All(
            moves,
            move => Assert.Equal(TestSupport.Square("e1"), move.From));
    }

    [Fact]
    public void KingIsNeverGeneratedAsAnOrdinaryCapture()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("a1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e7", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King));

        Assert.DoesNotContain(
            Move("e7", "e8"),
            game.GenerateMoves(TestSupport.Square("e7")));
    }

    [Fact]
    public void InitialPositionIsActive()
    {
        var status = Variant.CreateGame()
            .Status;

        Assert.Equal(StatusDefinitions.Active, status.Id);
        Assert.False(status.IsTerminal);
        Assert.Null(status.Outcome);
    }

    [Fact]
    public void FoolMateIsCheckmateAndPreventsFurtherExecution()
    {
        var game = CreateFoolsMate();
        var status = game.Status;
        var outcome = Assert.IsType<GameOutcome>(status.Outcome);

        Assert.Equal(StatusDefinitions.Checkmate, status.Id);
        Assert.Equal(TerminationDefinitions.Checkmate, outcome.Termination);
        Assert.True(status.IsTerminal);
        Assert.Equal([SideDefinitions.Black], outcome.Winners);
        Assert.Empty(TestSupport.AllMoves(game));
        Assert.Throws<InvalidOperationException>(() =>
            game.Execute(Move("e2", "e3")));
    }

    [Fact]
    public void KnownPositionIsStalemateWithoutWinner()
    {
        var game = TestSupport.CreateGame(
            SideDefinitions.Black,
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("c6", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At(
                "b6",
                SideDefinitions.White,
                PieceDefinitions.Queen));
        var status = game.Status;
        var outcome = Assert.IsType<GameOutcome>(status.Outcome);

        Assert.Equal(StatusDefinitions.Stalemate, status.Id);
        Assert.Equal(TerminationDefinitions.Stalemate, outcome.Termination);
        Assert.True(status.IsTerminal);
        Assert.Empty(outcome.Winners);
        Assert.Empty(TestSupport.AllMoves(game));
    }

    [Fact]
    public void CheckDetectorRejectsSideOutsideGameAndRequiresExactlyOneKing()
    {
        var detector = new CheckDetector(new PatternAttackGenerator());
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King));

        Assert.Throws<ArgumentException>(() => detector.IsInCheck(
            game.State,
            new Side("test:outsider")));

        var missingKing = TestSupport
            .CreateGameStateFactory(
                TurnOrderDefinition.Instance,
                TestSupport.At(
                    "e8",
                    SideDefinitions.Black,
                    PieceDefinitions.King))
            .Create();

        Assert.Throws<InvalidOperationException>(() =>
            detector.IsInCheck(missingKing, SideDefinitions.White));
    }

    internal static Game CreateFoolsMate()
    {
        var game = Variant.CreateGame();
        TestSupport.Play(game, "f2", "f3");
        TestSupport.Play(game, "e7", "e5");
        TestSupport.Play(game, "g2", "g4");
        TestSupport.Play(game, "d8", "h4");
        return game;
    }

    private static Move Move(
        string from,
        string to)
    {
        return new Move(TestSupport.Square(from), TestSupport.Square(to));
    }

    private static Game CreateKnightCheckGame(
        bool hasSecondCheckingLine)
    {
        var placements = new List<Placement>
        {
            TestSupport.At(
                "e1",
                SideDefinitions.White,
                PieceDefinitions.King),
            TestSupport.At(
                "g2",
                SideDefinitions.White,
                PieceDefinitions.Pawn),
            TestSupport.At(
                "a8",
                SideDefinitions.Black,
                PieceDefinitions.King),
            TestSupport.At(
                "f3",
                SideDefinitions.Black,
                PieceDefinitions.Knight)
        };

        if (hasSecondCheckingLine)
        {
            placements.Add(
                TestSupport.At(
                    "e8",
                    SideDefinitions.Black,
                    PieceDefinitions.Rook));
        }

        return TestSupport.CreateGame([.. placements]);
    }

    private static PieceDefinition ResolveDefinition(
        string name)
    {
        return name switch
        {
            "rook" => PieceDefinitions.Rook,
            "bishop" => PieceDefinitions.Bishop,
            "queen" => PieceDefinitions.Queen,
            "knight" => PieceDefinitions.Knight,
            "pawn" => PieceDefinitions.Pawn,
            "king" => PieceDefinitions.King,
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }
}
