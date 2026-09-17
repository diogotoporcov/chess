// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Board;
using Chess.Core.Games;
using Chess.Core.Games.Status;
using Chess.Core.Movement;
using Chess.Core.Notation;
using Chess.Core.Sides;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Notation.Uci;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests.Notation.Uci;

public sealed class UciMoveCodecTests
{
    private readonly UciMoveCodec _codec = new();

    [Theory]
    [InlineData("e2", "e4", "e2e4")]
    [InlineData("g1", "f3", "g1f3")]
    public void RoundTripsOrdinaryMovesWithoutMutation(
        string from,
        string to,
        string notation)
    {
        var game = Variant.CreateGame();
        var move = TestSupport.FindMove(game, from, to);

        AssertRoundTrip(game, StandardInitialState.Default, move, notation);
    }

    [Fact]
    public void ImplementsTheContextualNotationContract()
    {
        var codec = Assert.IsType<IContextualNotationCodec<Game, Move>>(
            _codec,
            exactMatch: false);
        var game = Variant.CreateGame();
        var move = TestSupport.FindMove(game, "e2", "e4");

        Assert.Equal("e2e4", codec.Format(game, move));
        Assert.Equal(move, codec.Parse(game, "e2e4"));
    }

    [Fact]
    public void RoundTripsAnOrdinaryCaptureWithoutAnOption()
    {
        var game = Variant.CreateGame();
        TestSupport.Play(game, "e2", "e4");
        TestSupport.Play(game, "d7", "d5");
        var move = TestSupport.FindMove(game, "e4", "d5");

        Assert.Null(move.OptionId);
        AssertRoundTrip(game, StandardInitialState.Default, move, "e4d5");
    }

    [Fact]
    public void RoundTripsEnPassantWithItsExactOptionWithoutMutation()
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

        AssertRoundTrip(game, StandardInitialState.Default, move, "e5d6");
        Assert.Equal(
            MoveOptions.EnPassant,
            _codec.Parse(game, "e5d6")
                .OptionId);
    }

    [Theory]
    [InlineData(false, "e1", "g1", "e1g1", true)]
    [InlineData(false, "e1", "c1", "e1c1", false)]
    [InlineData(true, "e8", "g8", "e8g8", true)]
    [InlineData(true, "e8", "c8", "e8c8", false)]
    public void RoundTripsStandardCastlingWithItsExactOption(
        bool blackToMove,
        string from,
        string to,
        string notation,
        bool kingSide)
    {
        var initialState = CreateCastlingState(
            blackToMove ? SideDefinitions.Black : SideDefinitions.White);
        var game = Variant.CreateGame(initialState);
        var option = kingSide
            ? MoveOptions.CastleKingSide
            : MoveOptions.CastleQueenSide;
        var move = TestSupport.FindMove(game, from, to, option);

        AssertRoundTrip(game, initialState, move, notation);
        Assert.Equal(
            option,
            _codec.Parse(game, notation)
                .OptionId);
    }

    [Fact]
    public void FormatRejectsCastlingCoordinatesWithoutTheCastlingOption()
    {
        var initialState = CreateCastlingState(SideDefinitions.White);
        var game = Variant.CreateGame(initialState);
        var move = new Move(TestSupport.Square("e1"), TestSupport.Square("g1"));

        var exception =
            Assert.Throws<ArgumentException>(() => _codec.Format(game, move));

        Assert.Equal("move", exception.ParamName);
    }

    [Theory]
    [InlineData("q")]
    [InlineData("r")]
    [InlineData("b")]
    [InlineData("n")]
    public void RoundTripsEveryQuietPromotion(
        string suffix)
    {
        var initialState = CreatePromotionState(includeCapture: false);
        var game = Variant.CreateGame(initialState);
        var option = PromotionOption(suffix[0]);
        var move = TestSupport.FindMove(game, "a7", "a8", option);

        AssertRoundTrip(game, initialState, move, "a7a8" + suffix);
        Assert.Equal(
            option,
            _codec.Parse(game, "a7a8" + suffix)
                .OptionId);
    }

    [Fact]
    public void RoundTripsCapturePromotion()
    {
        var initialState = CreatePromotionState(includeCapture: true);
        var game = Variant.CreateGame(initialState);
        var move = TestSupport.FindMove(
            game,
            "a7",
            "b8",
            PromotionOptions.Rook);

        AssertRoundTrip(game, initialState, move, "a7b8r");
    }

    [Theory]
    [InlineData("a7a8")]
    [InlineData("a7a8Q")]
    [InlineData("a7a8R")]
    [InlineData("a7a8B")]
    [InlineData("a7a8N")]
    [InlineData("a7a8k")]
    [InlineData("a7a8p")]
    public void RejectsInvalidPromotionForms(
        string notation)
    {
        var initialState = CreatePromotionState(includeCapture: false);
        var game = Variant.CreateGame(initialState);

        Assert.Throws<FormatException>(() => _codec.Parse(game, notation));
    }

    [Fact]
    public void RejectsPromotionSuffixOnAnOrdinaryMove()
    {
        var game = Variant.CreateGame();

        Assert.Throws<FormatException>(() => _codec.Parse(game, "e2e4q"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("e2")]
    [InlineData("e2e")]
    [InlineData("e2e4qz")]
    [InlineData("e2-e4")]
    [InlineData("e2 e4")]
    [InlineData("E2E4")]
    [InlineData("e2E4")]
    [InlineData(" e2e4")]
    [InlineData("e2e4 ")]
    [InlineData("e2e4\n")]
    [InlineData("e2e44")]
    [InlineData("abcdef")]
    [InlineData("0000")]
    [InlineData("O-O")]
    [InlineData("O-O-O")]
    [InlineData("i2e4")]
    [InlineData("a0a1")]
    [InlineData("a9a8")]
    [InlineData("e2i4")]
    public void RejectsMalformedOrUnsupportedNotation(
        string notation)
    {
        var game = Variant.CreateGame();

        Assert.Throws<FormatException>(() => _codec.Parse(game, notation));
    }

    [Theory]
    [InlineData("e2e5")]
    [InlineData("e1e3")]
    [InlineData("a1a8")]
    public void RejectsSyntacticallyValidIllegalMoves(
        string notation)
    {
        var game = Variant.CreateGame();

        Assert.Throws<FormatException>(() => _codec.Parse(game, notation));
    }

    [Fact]
    public void ResolvesOnlyMovesForTheCurrentSide()
    {
        var game = Variant.CreateGame();

        Assert.Throws<FormatException>(() => _codec.Parse(game, "e7e5"));

        TestSupport.Play(game, "e2", "e4");
        var move = TestSupport.FindMove(game, "e7", "e5");

        Assert.Equal(move, _codec.Parse(game, "e7e5"));
    }

    [Fact]
    public void WorksFromACustomInitialStateAndPreservesItsMetadata()
    {
        var initialState = TestSupport.CreateInitialState(
            SideDefinitions.White,
            [
                TestSupport.At(
                    "e1",
                    SideDefinitions.White,
                    PieceDefinitions.King),
                TestSupport.At(
                    "a1",
                    SideDefinitions.White,
                    PieceDefinitions.Rook),
                TestSupport.At(
                    "e8",
                    SideDefinitions.Black,
                    PieceDefinitions.King),
                TestSupport.At(
                    "a8",
                    SideDefinitions.Black,
                    PieceDefinitions.Rook)
            ],
            castlingRights: NoCastlingRights,
            halfmoveClock: 47,
            fullmoveNumber: 23);
        var game = Variant.CreateGame(initialState);
        var move = TestSupport.FindMove(game, "a1", "a2");

        AssertRoundTrip(game, initialState, move, "a1a2");
    }

    [Fact]
    public void RejectsParseAndFormatAfterTheGameHasEnded()
    {
        var game = TestSupport.CreateGame(
            TestSupport.At("f6", SideDefinitions.White, PieceDefinitions.King),
            TestSupport.At("g6", SideDefinitions.White, PieceDefinitions.Queen),
            TestSupport.At("h8", SideDefinitions.Black, PieceDefinitions.King));
        game.Execute(TestSupport.FindMove(game, "g6", "g7"));
        var move = new Move(TestSupport.Square("h8"), TestSupport.Square("h7"));

        Assert.True(game.Status.IsTerminal);
        Assert.Throws<FormatException>(() => _codec.Parse(game, "h8h7"));
        Assert.Throws<InvalidOperationException>(() =>
            _codec.Format(game, move));
    }

    [Fact]
    public void RejectsNullArguments()
    {
        var game = Variant.CreateGame();
        var move = TestSupport.FindMove(game, "e2", "e4");

        Assert.Throws<ArgumentNullException>(() => _codec.Parse(null!, "e2e4"));
        Assert.Throws<ArgumentNullException>(() => _codec.Parse(game, null!));
        Assert.Throws<ArgumentNullException>(() => _codec.Format(null!, move));
    }

    [Fact]
    public void FormatRejectsAnIllegalMoveWithTheMoveParameterName()
    {
        var game = Variant.CreateGame();
        var move = new Move(TestSupport.Square("e2"), TestSupport.Square("e5"));

        var exception =
            Assert.Throws<ArgumentException>(() => _codec.Format(game, move));

        Assert.Equal("move", exception.ParamName);
    }

    [Fact]
    public void FormatRejectsAnOutsideOriginWithTheMoveParameterName()
    {
        var game = Variant.CreateGame();
        var move = new Move(new Square(64), TestSupport.Square("e4"));

        var exception =
            Assert.Throws<ArgumentException>(() => _codec.Format(game, move));

        Assert.Equal("move", exception.ParamName);
    }

    [Fact]
    public void FormatRejectsAnOutsideDestinationWithTheMoveParameterName()
    {
        var game = Variant.CreateGame();
        var move = new Move(TestSupport.Square("e2"), new Square(64));

        var exception =
            Assert.Throws<ArgumentException>(() => _codec.Format(game, move));

        Assert.Equal("move", exception.ParamName);
    }

    [Fact]
    public void FormatRejectsAnUnsupportedMoveOption()
    {
        var game = Variant.CreateGame();
        var move = new Move(
            TestSupport.Square("e2"),
            TestSupport.Square("e4"),
            new MoveOptionId("test:unsupported"));

        var exception =
            Assert.Throws<ArgumentException>(() => _codec.Format(game, move));

        Assert.Equal("move", exception.ParamName);
    }

    private void AssertRoundTrip(
        Game game,
        StandardInitialState initialState,
        Move move,
        string expected)
    {
        var snapshot = UciGameSnapshot.Capture(game, initialState);

        Assert.Equal(expected, _codec.Format(game, move));
        snapshot.AssertMatches(game, initialState);

        var parsed = _codec.Parse(game, expected);

        Assert.Equal(move, parsed);
        Assert.Equal(move.OptionId, parsed.OptionId);
        Assert.Equal(move, _codec.Parse(game, _codec.Format(game, move)));
        snapshot.AssertMatches(game, initialState);
    }

    private static StandardInitialState CreateCastlingState(
        Side sideToMove)
    {
        return TestSupport.CreateInitialState(
            sideToMove,
            [
                TestSupport.At(
                    "e1",
                    SideDefinitions.White,
                    PieceDefinitions.King),
                TestSupport.At(
                    "a1",
                    SideDefinitions.White,
                    PieceDefinitions.Rook),
                TestSupport.At(
                    "h1",
                    SideDefinitions.White,
                    PieceDefinitions.Rook),
                TestSupport.At(
                    "e8",
                    SideDefinitions.Black,
                    PieceDefinitions.King),
                TestSupport.At(
                    "a8",
                    SideDefinitions.Black,
                    PieceDefinitions.Rook),
                TestSupport.At(
                    "h8",
                    SideDefinitions.Black,
                    PieceDefinitions.Rook)
            ],
            new CastlingRights(true, true, true, true));
    }

    private static StandardInitialState CreatePromotionState(
        bool includeCapture)
    {
        var placements = new List<Placement>
        {
            TestSupport.At(
                "e1",
                SideDefinitions.White,
                PieceDefinitions.King),
            TestSupport.At(
                "h8",
                SideDefinitions.Black,
                PieceDefinitions.King),
            TestSupport.At(
                "a7",
                SideDefinitions.White,
                PieceDefinitions.Pawn)
        };

        if (includeCapture)
        {
            placements.Add(
                TestSupport.At(
                    "b8",
                    SideDefinitions.Black,
                    PieceDefinitions.Rook));
        }

        return TestSupport.CreateInitialState(
            SideDefinitions.White,
            placements,
            NoCastlingRights);
    }

    private static MoveOptionId PromotionOption(
        char suffix)
    {
        return suffix switch
        {
            'q' => PromotionOptions.Queen,
            'r' => PromotionOptions.Rook,
            'b' => PromotionOptions.Bishop,
            'n' => PromotionOptions.Knight,
            _ => throw new ArgumentOutOfRangeException(nameof(suffix))
        };
    }

    private static CastlingRights NoCastlingRights =>
        new(false, false, false, false);

    private sealed record UciGameSnapshot(
        StandardGameSnapshot Game,
        StandardPositionFacts Facts,
        GameStatusId StatusId,
        GameOutcome? Outcome)
    {
        public static UciGameSnapshot Capture(
            Game game,
            StandardInitialState initialState)
        {
            var facts = TestSupport
                .CreatePositionFactsEvaluator(initialState)
                .Evaluate(game.State);
            var status = game.Status;

            return new UciGameSnapshot(
                StandardGameSnapshot.Capture(game),
                facts,
                status.Id,
                status.Outcome);
        }

        public void AssertMatches(
            Game game,
            StandardInitialState initialState)
        {
            Game.AssertMatches(game);

            var actualFacts = TestSupport
                .CreatePositionFactsEvaluator(initialState)
                .Evaluate(game.State);
            var actualStatus = game.Status;

            Assert.Equal(Facts, actualFacts);
            Assert.Equal(StatusId, actualStatus.Id);
            Assert.Equal(Outcome, actualStatus.Outcome);
        }
    }
}
