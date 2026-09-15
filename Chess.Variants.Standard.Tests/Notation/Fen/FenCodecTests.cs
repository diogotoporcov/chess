// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games.Variants;
using Chess.Core.Notation;
using Chess.Variants.Standard.Board;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Notation.Fen;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests.Notation.Fen;

public sealed class FenCodecTests
{
    private const string StartingFen =
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR " + "w KQkq - 0 1";

    private readonly FenCodec _codec = new();

    [Fact]
    public void ImplementsGenericNotationCodec()
    {
        INotationCodec<StandardInitialState> codec = new FenCodec();

        var state = codec.Parse(StartingFen);
        var notation = codec.Format(state);

        Assert.Equal(StartingFen, notation);
    }

    [Fact]
    public void DefaultStartingPositionHasCanonicalFen()
    {
        Assert.Equal(StartingFen, _codec.Format(StandardInitialState.Default));
    }

    [Fact]
    public void StartingFenCreatesCanonicalGame()
    {
        var game = Variant.CreateGame(_codec.Parse(StartingFen));

        Assert.Same(SideDefinitions.White, game.State.CurrentSide);
        Assert.Equal(
            20,
            TestSupport.AllMoves(game)
                .Count);
        Assert.Equal(StatusDefinitions.Active, game.Status.Id);
        Assert.Null(game.Outcome);
    }

    [Fact]
    public void AcceptedWhitespaceCanonicalizesOnRoundTrip()
    {
        const string input = "  rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR" +
                             "   w\tKQkq   -\r\n0   1  ";

        Assert.Equal(StartingFen, _codec.Format(_codec.Parse(input)));
    }

    [Fact]
    public void DomainRoundTripPreservesAllStateValues()
    {
        const string fen = "r3k2r/8/8/8/8/8/8/R3K2R b Kq - 87 37";
        var original = _codec.Parse(fen);

        var parsed = _codec.Parse(_codec.Format(original));

        AssertEquivalent(original, parsed);
    }

    [Fact]
    public void BlackToMoveKeepsCanonicalTurnOrderAndTransitionsToWhite()
    {
        var initialState = _codec.Parse("r3k3/8/8/8/8/8/8/R3K3 b - - 12 37");
        var game = Variant.CreateGame(initialState);

        Assert.Equal(
            [SideDefinitions.White, SideDefinitions.Black],
            game.State.TurnOrder.Sides);
        Assert.Same(SideDefinitions.Black, game.State.CurrentSide);

        TestSupport.Play(game, "a8", "a7");

        Assert.Same(SideDefinitions.White, game.State.CurrentSide);
    }

    [Fact]
    public void CastlingFieldDoesNotInferRightsFromHomePieces()
    {
        var state = _codec.Parse("4k3/8/8/8/8/8/8/4K2R w - - 0 1");

        Assert.Equal(default, state.CastlingRights);
    }

    [Fact]
    public void DeclaredCastlingRightIsPreserved()
    {
        var state = _codec.Parse("4k3/8/8/8/8/8/8/4K2R w K - 0 1");

        Assert.True(state.CastlingRights.WhiteKingSide);
        Assert.False(state.CastlingRights.WhiteQueenSide);
        Assert.False(state.CastlingRights.BlackKingSide);
        Assert.False(state.CastlingRights.BlackQueenSide);
    }

    [Fact]
    public void StructurallyImpossibleCastlingRightIsFormattingError()
    {
        var exception = Assert.Throws<FormatException>(() => _codec.Parse(
            "4k3/8/8/8/8/8/8/4K3 w K - 0 1"));

        Assert.IsType<ArgumentException>(
            exception.InnerException,
            exactMatch: false);
    }

    [Theory]
    [InlineData("KQkq")]
    [InlineData("KQ")]
    [InlineData("Kq")]
    [InlineData("kq")]
    [InlineData("Q")]
    [InlineData("-")]
    public void CanonicalCastlingSubsetsAreAcceptedAndFormatted(
        string castling)
    {
        var fen = StartingFen.Replace("KQkq", castling);

        Assert.Equal(fen, _codec.Format(_codec.Parse(fen)));
    }

    [Fact]
    public void LostCastlingRightsAreReflectedInCurrentFacts()
    {
        var initialState = _codec.Parse("4k3/8/8/8/8/8/8/4K2R w K - 0 1");
        var game = Variant.CreateGame(initialState);
        var evaluator = TestSupport.CreatePositionFactsEvaluator(initialState);

        TestSupport.Play(game, "h1", "h2");
        TestSupport.Play(game, "e8", "e7");
        TestSupport.Play(game, "h2", "h1");
        TestSupport.Play(game, "e7", "e8");

        Assert.Equal(
            "4k3/8/8/8/8/8/8/4K2R w - - 4 3",
            _codec.Format(evaluator.Evaluate(game.State)));
    }

    [Fact]
    public void RawEnPassantWithoutCapturerRoundTripsAndStaysOutOfKey()
    {
        const string fen = "4k3/8/8/3p4/8/8/8/4K3 w - d6 0 1";
        var initialState = _codec.Parse(fen);
        var game = Variant.CreateGame(initialState);
        var facts = TestSupport
            .CreatePositionFactsEvaluator(initialState)
            .Evaluate(game.State);

        Assert.Equal(TestSupport.Square("d6"), initialState.EnPassantTarget);
        Assert.Equal(TestSupport.Square("d6"), facts.EnPassantTarget);
        Assert.Null(facts.EffectiveEnPassantTarget);
        Assert.Equal(fen, _codec.Format(initialState));
        Assert.Equal(fen, _codec.Format(facts));
    }

    [Fact]
    public void LegallyCapturableEnPassantHasRawAndEffectiveTargets()
    {
        const string fen = "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1";
        var initialState = _codec.Parse(fen);
        var game = Variant.CreateGame(initialState);
        var facts = TestSupport
            .CreatePositionFactsEvaluator(initialState)
            .Evaluate(game.State);

        Assert.Equal(TestSupport.Square("d6"), facts.EnPassantTarget);
        Assert.Equal(TestSupport.Square("d6"), facts.EffectiveEnPassantTarget);
        Assert.Equal(fen, _codec.Format(facts));
    }

    [Fact]
    public void OrdinaryMoveExpiresCurrentRawEnPassantTarget()
    {
        var initialState = _codec.Parse("4k3/8/8/3p4/8/8/8/4K3 w - d6 0 1");
        var game = Variant.CreateGame(initialState);
        var evaluator = TestSupport.CreatePositionFactsEvaluator(initialState);

        TestSupport.Play(game, "e1", "f1");

        Assert.Equal(
            "4k3/8/8/3p4/8/8/8/5K2 b - - 1 1",
            _codec.Format(evaluator.Evaluate(game.State)));
    }

    [Fact]
    public void DoublePawnPushFormatsRawTargetWithoutLegalCapturer()
    {
        var initialState = StandardInitialState.Default;
        var game = Variant.CreateGame(initialState);
        var evaluator = TestSupport.CreatePositionFactsEvaluator(initialState);

        TestSupport.Play(game, "e2", "e4");

        var facts = evaluator.Evaluate(game.State);

        Assert.Null(facts.EffectiveEnPassantTarget);
        Assert.Equal(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR " + "b KQkq e3 0 1",
            _codec.Format(facts));
    }

    [Fact]
    public void CustomCountersRoundTripAndAdvanceWithCurrentFacts()
    {
        const string fen = "r3k3/8/8/8/8/8/8/R3K3 w - - 87 37";
        var initialState = _codec.Parse(fen);
        var roundTripped = _codec.Parse(_codec.Format(initialState));
        var game = Variant.CreateGame(roundTripped);
        var evaluator = TestSupport.CreatePositionFactsEvaluator(roundTripped);

        Assert.Equal(87, roundTripped.HalfmoveClock);
        Assert.Equal(37, roundTripped.FullmoveNumber);

        TestSupport.Play(game, "a1", "a2");
        var afterWhite = evaluator.Evaluate(game.State);
        Assert.Equal(88, afterWhite.HalfmoveClock);
        Assert.Equal(37, afterWhite.FullmoveNumber);

        TestSupport.Play(game, "a8", "a7");
        var afterBlack = evaluator.Evaluate(game.State);
        Assert.Equal(89, afterBlack.HalfmoveClock);
        Assert.Equal(38, afterBlack.FullmoveNumber);
        Assert.Equal(
            "4k3/r7/8/8/8/8/R7/4K3 w - - 89 38",
            _codec.Format(afterBlack));
    }

    [Fact]
    public void PawnMoveResetsLoadedHalfmoveClock()
    {
        var initialState = _codec.Parse("4k3/8/8/8/8/8/4P3/4K3 w - - 87 37");
        var game = Variant.CreateGame(initialState);
        var evaluator = TestSupport.CreatePositionFactsEvaluator(initialState);

        TestSupport.Play(game, "e2", "e3");

        Assert.Equal(
            0,
            evaluator.Evaluate(game.State)
                .HalfmoveClock);
    }

    [Fact]
    public void CaptureResetsLoadedHalfmoveClock()
    {
        var initialState = _codec.Parse("r3k3/8/8/8/8/8/8/R3K3 w - - 87 37");
        var game = Variant.CreateGame(initialState);
        var evaluator = TestSupport.CreatePositionFactsEvaluator(initialState);

        TestSupport.Play(game, "a1", "a8");

        Assert.Equal(
            0,
            evaluator.Evaluate(game.State)
                .HalfmoveClock);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \t\r\n")]
    [InlineData("8/8/8/8/8/8/8/8")]
    [InlineData("8/8/8/8/8/8/8/8 w")]
    [InlineData("8/8/8/8/8/8/8/8 w -")]
    [InlineData("8/8/8/8/8/8/8/8 w - -")]
    [InlineData("8/8/8/8/8/8/8/8 w - - 0")]
    [InlineData("8/8/8/8/8/8/8/8 w - - 0 1 extra")]
    [InlineData("8/8/8/8/8/8/8/8 w - - 0 1 extra fields")]
    public void RequiresExactlySixFields(
        string fen)
    {
        Assert.Throws<FormatException>(() => _codec.Parse(fen));
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/4K3")]
    [InlineData("4k3/8/8/8/8/8/8/4K3/8")]
    [InlineData("4k2/8/8/8/8/8/8/4K3")]
    [InlineData("4k4/8/8/8/8/8/8/4K3")]
    [InlineData("4k3//8/8/8/8/8/4K3")]
    [InlineData("4k30/8/8/8/8/8/8/4K3")]
    [InlineData("4k9/8/8/8/8/8/8/4K3")]
    [InlineData("31k3/8/8/8/8/8/8/4K3")]
    [InlineData("4x3/8/8/8/8/8/8/4K3")]
    [InlineData("4k3\\8/8/8/8/8/8/4K3")]
    public void RejectsMalformedPiecePlacement(
        string placement)
    {
        Assert.Throws<FormatException>(() =>
            _codec.Parse($"{placement} w - - 0 1"));
    }

    [Theory]
    [InlineData("W")]
    [InlineData("B")]
    [InlineData("white")]
    [InlineData("black")]
    [InlineData("x")]
    [InlineData("-")]
    public void RejectsMalformedActiveColor(
        string activeColor)
    {
        Assert.Throws<FormatException>(() =>
            _codec.Parse(StartingFen.Replace(" w ", $" {activeColor} ")));
    }

    [Theory]
    [InlineData("KK")]
    [InlineData("qq")]
    [InlineData("K-")]
    [InlineData("-K")]
    [InlineData("qk")]
    [InlineData("QK")]
    [InlineData("A")]
    [InlineData("HAha")]
    public void RejectsMalformedCastlingField(
        string castling)
    {
        Assert.Throws<FormatException>(() =>
            _codec.Parse(StartingFen.Replace("KQkq", castling)));
    }

    [Theory]
    [InlineData("A3")]
    [InlineData("e4")]
    [InlineData("e5")]
    [InlineData("i3")]
    [InlineData("a9")]
    [InlineData("--")]
    [InlineData("abc")]
    public void RejectsMalformedEnPassantField(
        string enPassant)
    {
        Assert.Throws<FormatException>(() => _codec.Parse(
            $"4k3/8/8/8/8/8/8/4K3 w - {enPassant} 0 1"));
    }

    [Fact]
    public void SemanticEnPassantFailureIsFormattingErrorWithInnerException()
    {
        var exception = Assert.Throws<FormatException>(() => _codec.Parse(
            "4k3/8/8/8/8/8/8/4K3 w - d6 0 1"));

        Assert.IsType<ArgumentException>(
            exception.InnerException,
            exactMatch: false);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("text")]
    [InlineData("2147483648")]
    public void RejectsMalformedHalfmoveClock(
        string halfmoveClock)
    {
        Assert.Throws<FormatException>(() => _codec.Parse(
            $"4k3/8/8/8/8/8/8/4K3 w - - {halfmoveClock} 1"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("00")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("text")]
    [InlineData("2147483648")]
    public void RejectsMalformedFullmoveNumber(
        string fullmoveNumber)
    {
        Assert.Throws<FormatException>(() => _codec.Parse(
            $"4k3/8/8/8/8/8/8/4K3 w - - 0 {fullmoveNumber}"));
    }

    [Theory]
    [InlineData("01", 1)]
    [InlineData("00037", 37)]
    public void AcceptsFullmoveNumberWithLeadingZeroes(
        string fullmoveNumber,
        int expected)
    {
        var state = _codec.Parse(
            $"4k3/8/8/8/8/8/8/4K3 w - - 0 {fullmoveNumber}");

        Assert.Equal(expected, state.FullmoveNumber);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("00")]
    [InlineData("17")]
    [InlineData("00017")]
    [InlineData("149")]
    [InlineData("150")]
    public void AcceptsNonNegativeHalfmoveClock(
        string halfmoveClock)
    {
        var state = _codec.Parse(
            $"4k3/8/8/8/8/8/8/4K3 w - - {halfmoveClock} 1");

        Assert.Equal(int.Parse(halfmoveClock), state.HalfmoveClock);
    }

    [Fact]
    public void PaddedCountersCanonicalizeOnFormatting()
    {
        var state = _codec.Parse("4k3/8/8/8/8/8/8/4K3 w - - 00017 00037");

        Assert.Equal("4k3/8/8/8/8/8/8/4K3 w - - 17 37", _codec.Format(state));
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/8 w - - 0 1")]
    [InlineData("8/8/8/8/8/8/8/4K3 w - - 0 1")]
    [InlineData("P3k3/8/8/8/8/8/8/4K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w K - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - d6 0 1")]
    public void DomainInvalidFenIsFormattingErrorWithInnerException(
        string fen)
    {
        var exception = Assert.Throws<FormatException>(() => _codec.Parse(fen));

        Assert.IsType<ArgumentException>(
            exception.InnerException,
            exactMatch: false);
    }

    [Theory]
    [InlineData("variant:white", "chess:king")]
    [InlineData("chess:white", "variant:piece")]
    public void FactsWithUnsupportedPlacementIdsFailClearly(
        string sideId,
        string pieceDefinitionId)
    {
        var key = new StandardPositionKey(
            [
                new StandardPiecePlacement(
                    BoardLayout.GetSquare(7, 4),
                    sideId,
                    pieceDefinitionId)
            ],
            SideDefinitions.White.Id,
            default,
            null);
        var facts = new StandardPositionFacts(key, null, 0, 1);

        Assert.Throws<InvalidOperationException>(() => _codec.Format(facts));
    }

    [Fact]
    public void FactsWithUnsupportedSideToMoveIdFailClearly()
    {
        var key = new StandardPositionKey([], "variant:side", default, null);
        var facts = new StandardPositionFacts(key, null, 0, 1);

        Assert.Throws<InvalidOperationException>(() => _codec.Format(facts));
    }

    [Fact]
    public void NullInputsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => _codec.Parse(null!));
        Assert.Throws<ArgumentNullException>(() =>
            _codec.Format((StandardInitialState)null!));
        Assert.Throws<ArgumentNullException>(() =>
            _codec.Format((StandardPositionFacts)null!));
    }

    private static void AssertEquivalent(
        StandardInitialState expected,
        StandardInitialState actual)
    {
        var expectedPlacements = expected
            .Placements
            .Select(GetPlacementIdentity)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualPlacements = actual
            .Placements
            .Select(GetPlacementIdentity)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedPlacements, actualPlacements);
        Assert.Same(expected.SideToMove, actual.SideToMove);
        Assert.Equal(expected.CastlingRights, actual.CastlingRights);
        Assert.Equal(expected.EnPassantTarget, actual.EnPassantTarget);
        Assert.Equal(expected.HalfmoveClock, actual.HalfmoveClock);
        Assert.Equal(expected.FullmoveNumber, actual.FullmoveNumber);
    }

    private static string GetPlacementIdentity(
        InitialPiecePlacement placement)
    {
        return string.Join(
            ':',
            BoardLayout.GetRow(placement.Square),
            BoardLayout.GetColumn(placement.Square),
            placement.Side.Id,
            placement.Definition.Id.Value);
    }
}
