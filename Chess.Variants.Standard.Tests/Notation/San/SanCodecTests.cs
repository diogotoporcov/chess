// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Games.Variants;
using Chess.Core.Movement;
using Chess.Core.Notation;
using Chess.Core.Pieces;
using Chess.Core.Sides;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Notation.San;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests.Notation.San;

public sealed class SanCodecTests
{
    private readonly SanCodec _codec = new();

    [Fact]
    public void ImplementsContextualNotationCodec()
    {
        IContextualNotationCodec<Game, Move> codec = new SanCodec();
        var game = Variant.CreateGame();
        var move = TestSupport.FindMove(game, "e2", "e4");

        Assert.Equal(move, codec.Parse(game, codec.Format(game, move)));
    }

    [Fact]
    public void FormatsAndParsesBasicPawnAndPieceMovesWithoutMutation()
    {
        var game = Variant.CreateGame();
        var e4 = TestSupport.FindMove(game, "e2", "e4");
        AssertRoundTrip(game, e4, "e4");
        game.Execute(e4);
        game.Execute(TestSupport.FindMove(game, "e7", "e5"));
        AssertRoundTrip(game, TestSupport.FindMove(game, "g1", "f3"), "Nf3");
    }

    [Fact]
    public void FormatsAndParsesCapturesIncludingEnPassant()
    {
        var bishopGame = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At(
                "c4",
                SideDefinitions.White,
                PieceDefinitions.Bishop),
            TestSupport.At("e6", SideDefinitions.Black, PieceDefinitions.Pawn));
        AssertRoundTrip(
            bishopGame,
            TestSupport.FindMove(bishopGame, "c4", "e6"),
            "Bxe6");

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
        AssertRoundTrip(game, move, "exd6");
        Assert.Equal(
            MoveOptions.EnPassant,
            _codec.Parse(game, "exd6")
                .OptionId);
        Assert.Throws<FormatException>(() => _codec.Parse(game, "exd6 e.p."));
    }

    [Fact]
    public void FormatsAndParsesOrdinaryPawnCaptureWithoutAnEnPassantOption()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e4", SideDefinitions.White, PieceDefinitions.Pawn),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("d5", SideDefinitions.Black, PieceDefinitions.Pawn));
        var move = TestSupport.FindMove(game, "e4", "d5");

        AssertRoundTrip(game, move, "exd5");
        Assert.Null(
            _codec.Parse(game, "exd5")
                .OptionId);
    }

    [Fact]
    public void FormatsAndParsesCastlingAndRejectsZeroNotation()
    {
        var game = CreateCastlingGame();
        var kingSide = TestSupport.FindMove(
            game,
            "e1",
            "g1",
            MoveOptions.CastleKingSide);
        AssertRoundTrip(game, kingSide, "O-O");
        Assert.Equal(
            MoveOptions.CastleKingSide,
            _codec.Parse(game, "O-O")
                .OptionId);

        var queenSideGame = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("a1", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.King));
        var queenSide = TestSupport.FindMove(
            queenSideGame,
            "e1",
            "c1",
            MoveOptions.CastleQueenSide);
        AssertRoundTrip(queenSideGame, queenSide, "O-O-O");
        Assert.Equal(
            MoveOptions.CastleQueenSide,
            _codec.Parse(queenSideGame, "O-O-O")
                .OptionId);
        Assert.Throws<FormatException>(() => _codec.Parse(game, "0-0"));
        Assert.Throws<FormatException>(() => _codec.Parse(
            queenSideGame,
            "0-0-0"));
    }

    [Fact]
    public void FormatsAndParsesEveryPromotionOptionAndCapturePromotion()
    {
        foreach (var option in PromotionOptions.All)
        {
            var game = CreatePromotionGame();
            var move = TestSupport.FindMove(game, "a7", "a8", option);
            var notation = _codec.Format(game, move);
            Assert.Contains(
                "=" + Symbol(option),
                notation,
                StringComparison.Ordinal);
            Assert.Equal(
                option,
                _codec.Parse(game, notation)
                    .OptionId);
        }

        var captureGame = CreatePromotionGame();
        var capture = TestSupport.FindMove(
            captureGame,
            "a7",
            "b8",
            PromotionOptions.Queen);
        AssertRoundTrip(captureGame, capture, "axb8=Q+");
        Assert.Throws<FormatException>(() => _codec.Parse(captureGame, "a8Q"));
    }

    [Fact]
    public void UsesFileRankAndSquareDisambiguationFromLegalMovesOnly()
    {
        var fileGame = CreateGame(
            ("c3", SideDefinitions.White, PieceDefinitions.Knight),
            ("g3", SideDefinitions.White, PieceDefinitions.Knight));
        AssertRoundTrip(
            fileGame,
            TestSupport.FindMove(fileGame, "c3", "e2"),
            "Nce2");
        Assert.Throws<FormatException>(() => _codec.Parse(fileGame, "Ne2"));

        var rankGame = CreateGame(
            ("e1", SideDefinitions.White, PieceDefinitions.Rook),
            ("e3", SideDefinitions.White, PieceDefinitions.Rook));
        AssertRoundTrip(
            rankGame,
            TestSupport.FindMove(rankGame, "e1", "e2"),
            "R1e2");

        var squareGame = CreateGame(
            ("c3", SideDefinitions.White, PieceDefinitions.Knight),
            ("c5", SideDefinitions.White, PieceDefinitions.Knight),
            ("g3", SideDefinitions.White, PieceDefinitions.Knight));
        AssertRoundTrip(
            squareGame,
            TestSupport.FindMove(squareGame, "c3", "e4"),
            "Nc3e4");

        var pinnedGame = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At(
                "b4",
                SideDefinitions.Black,
                PieceDefinitions.Bishop),
            TestSupport.At(
                "c3",
                SideDefinitions.White,
                PieceDefinitions.Knight),
            TestSupport.At(
                "g1",
                SideDefinitions.White,
                PieceDefinitions.Knight));
        AssertRoundTrip(
            pinnedGame,
            TestSupport.FindMove(pinnedGame, "g1", "e2"),
            "Ne2");
    }

    [Fact]
    public void FormatsCaptureDisambiguationBeforeTheCaptureMarker()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("a1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("a3", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("h3", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At(
                "e3",
                SideDefinitions.Black,
                PieceDefinitions.Knight),
            TestSupport.At("g8", SideDefinitions.Black, PieceDefinitions.King));
        var move = TestSupport.FindMove(game, "a3", "e3");

        AssertRoundTrip(game, move, "Raxe3");
        Assert.Throws<FormatException>(() => _codec.Parse(game, "Rxe3"));
    }

    [Fact]
    public void FormatsAndParsesCheckAndCheckmateWithRequiredSuffixes()
    {
        var checkGame = TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("a1", SideDefinitions.White, PieceDefinitions.Rook));
        AssertRoundTrip(
            checkGame,
            TestSupport.FindMove(checkGame, "a1", "a7"),
            "Ra7+");
        Assert.Throws<FormatException>(() => _codec.Parse(checkGame, "Ra7"));
        Assert.Throws<FormatException>(() => _codec.Parse(checkGame, "Ra7#"));

        var mateGame = CreateMateGame();
        AssertRoundTrip(
            mateGame,
            TestSupport.FindMove(mateGame, "g6", "g7"),
            "Qg7#");
        Assert.Throws<FormatException>(() => _codec.Parse(mateGame, "Qg7+"));
    }

    [Fact]
    public void RejectsCheckmatingSanWithoutTheMateSuffix()
    {
        var game = CreateMateGame();

        Assert.Throws<FormatException>(() => _codec.Parse(game, "Qg7"));
    }

    [Fact]
    public void
        UsesPositionalCheckSuffixWhenTheMoveAlsoReachesAnAutomaticOutcome()
    {
        var initialState = new StandardInitialState(
            [
                new InitialPiecePlacement(
                    TestSupport.Square("e1"),
                    SideDefinitions.White,
                    PieceDefinitions.King),
                new InitialPiecePlacement(
                    TestSupport.Square("a1"),
                    SideDefinitions.White,
                    PieceDefinitions.Rook),
                new InitialPiecePlacement(
                    TestSupport.Square("a8"),
                    SideDefinitions.Black,
                    PieceDefinitions.King)
            ],
            SideDefinitions.White,
            default,
            null,
            149,
            1);
        var game = Variant.CreateGame(initialState);
        var move = TestSupport.FindMove(game, "a1", "a7");

        AssertRoundTrip(game, move, "Ra7+");

        game.Execute(move);
        Assert.Equal(StatusDefinitions.Check, game.Status.Id);
        Assert.NotNull(game.Outcome);
    }

    [Theory]
    [InlineData("nf3")]
    [InlineData("Pe4")]
    [InlineData("Ng1f3")]
    [InlineData("Nf3++")]
    [InlineData("Nf3!")]
    [InlineData(" Nf3")]
    [InlineData("Nf3 ")]
    public void RejectsNonCanonicalSan(
        string notation)
    {
        var game = Variant.CreateGame();
        TestSupport.Play(game, "e2", "e4");
        TestSupport.Play(game, "e7", "e5");

        Assert.Throws<FormatException>(() => _codec.Parse(game, notation));
    }

    [Fact]
    public void RejectsIllegalMovesAndTerminalContexts()
    {
        var game = Variant.CreateGame();
        Assert.Throws<ArgumentException>(() => _codec.Format(
            game,
            new Move(TestSupport.Square("e2"), TestSupport.Square("e5"))));
        Assert.Throws<FormatException>(() => _codec.Parse(game, "Qh5"));

        var mateGame = TestSupport.CreateGame(
            TestSupport.At("f6", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("g6", SideDefinitions.White, PieceDefinitions.Queen),
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.King));
        mateGame.Execute(TestSupport.FindMove(mateGame, "g6", "g7"));
        Assert.Throws<InvalidOperationException>(() => _codec.Format(
            mateGame,
            new Move(TestSupport.Square("h8"), TestSupport.Square("h7"))));
        Assert.Throws<FormatException>(() => _codec.Parse(mateGame, "Kh7"));
    }

    private void AssertRoundTrip(
        Game game,
        Move move,
        string expected)
    {
        var snapshot = StandardGameSnapshot.Capture(game);
        Assert.Equal(expected, _codec.Format(game, move));
        snapshot.AssertMatches(game);
        Assert.Equal(move, _codec.Parse(game, expected));
        snapshot.AssertMatches(game);
    }

    private static Game CreateCastlingGame()
    {
        return TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("h1", SideDefinitions.White, PieceDefinitions.Rook),
            TestSupport.At("a8", SideDefinitions.Black, PieceDefinitions.King));
    }

    private static Game CreatePromotionGame()
    {
        return TestSupport.CreateGame(
            TestSupport.At("e1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("e8", SideDefinitions.Black, PieceDefinitions.King),
            TestSupport.At("a7", SideDefinitions.White, PieceDefinitions.Pawn),
            TestSupport.At("b8", SideDefinitions.Black, PieceDefinitions.Rook));
    }

    private static Game CreateMateGame()
    {
        return TestSupport.CreateGame(
            TestSupport.At("f6", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("g6", SideDefinitions.White, PieceDefinitions.Queen),
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.King));
    }

    private static Game CreateGame(
        params (string Square, Side Side, PieceDefinition Definition)[] pieces)
    {
        return TestSupport.CreateGame(
        [
            TestSupport.At("a1", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.King),
            .. pieces.Select(piece => TestSupport.At(
                piece.Square,
                piece.Side,
                piece.Definition))
        ]);
    }

    private static char Symbol(
        MoveOptionId option)
    {
        return option == PromotionOptions.Queen ? 'Q' :
            option == PromotionOptions.Rook ? 'R' :
            option == PromotionOptions.Bishop ? 'B' : 'N';
    }
}
