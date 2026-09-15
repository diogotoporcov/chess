// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Movement;
using Chess.Core.Notation;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Notation.Pgn;

namespace Chess.Variants.Standard.Tests.Notation.Pgn;

public sealed class PgnCodecTests
{
    private readonly PgnCodec _codec = new();

    [Fact]
    public void ImplementsGenericNotationCodec()
    {
        INotationCodec<PgnGame> codec = new PgnCodec();
        Assert.Equal(
            "*",
            codec
                .Format(codec.Parse(Headers + "\n*\n"))
                .Split("\n\n", StringSplitOptions.None)[1]);
    }

    [Fact]
    public void FormatsAndParsesStandardMainlineCanonically()
    {
        var pgn = Headers + "\n1.e4 e5 2.Nf3 Nc6 *";
        var formatted = _codec.Format(_codec.Parse(pgn));

        Assert.Equal(Headers + "\n1. e4 e5 2. Nf3 Nc6 *\n\n", formatted);
        Assert.DoesNotContain("SetUp", formatted);
    }

    [Theory]
    [InlineData("[Event \"Example\"]")]
    [InlineData("[Event\"Example\"]")]
    [InlineData("[ Event \"Example\" ]")]
    [InlineData("[\nEvent\n\"Example\"\n]")]
    public void AcceptsFlexibleTagPairWhitespace(
        string eventTag)
    {
        var parsed = _codec.Parse(
            Headers.Replace("[Event \"Event\"]", eventTag) + "\n*");

        Assert.Equal("Example", parsed.Event);
        Assert.Contains("[Event \"Example\"]", _codec.Format(parsed));
    }

    [Theory]
    [InlineData("e4 e5 *")]
    [InlineData("1 e4 e5 *")]
    [InlineData("1.e4 e5 *")]
    [InlineData("1 . e4 e5 *")]
    [InlineData("1.... e4 e5 *")]
    [InlineData("1. e4 1. e5 *")]
    [InlineData("1. e4 1... e5 *")]
    public void AcceptsFlexibleMoveNumberIndications(
        string movetext)
    {
        var parsed = _codec.Parse(Headers + "\n" + movetext);

        Assert.Equal(
            "1. e4 e5 *",
            _codec
                .Format(parsed)
                .Split("\n\n", StringSplitOptions.None)[1]);
    }

    [Fact]
    public void UsesDefaultStateForMissingSetUpAndFen()
    {
        Assert.Same(
            StandardInitialState.Default,
            _codec.Parse(Headers + "\n*")
                .InitialState);
    }

    [Fact]
    public void CanonicalizesSetUpZeroToDefaultState()
    {
        var parsed = _codec.Parse(Headers + "[SetUp \"0\"]\n\n*");

        Assert.Same(StandardInitialState.Default, parsed.InitialState);
        Assert.DoesNotContain("SetUp", _codec.Format(parsed));
    }

    [Theory]
    [InlineData("[SetUp \"1\"]\n")]
    [InlineData("[FEN \"4k3/8/8/8/8/8/8/4K3 w - - 0 1\"]\n")]
    [InlineData("[SetUp \"0\"]\n[FEN \"4k3/8/8/8/8/8/8/4K3 w - - 0 1\"]\n")]
    [InlineData("[SetUp \"invalid\"]\n")]
    public void RejectsInvalidSetUpFenCombinations(
        string tags)
    {
        Assert.Throws<FormatException>(() =>
            _codec.Parse(Headers + tags + "\n*"));
    }

    [Fact]
    public void SupportsCustomBlackStartAndSortedGeneratedTags()
    {
        const string fen = "4k3/8/8/8/8/8/8/4K2R b - - 0 37";
        var game = _codec.Parse(
            Headers.Replace("[Result \"*\"]", "[Result \"1/2-1/2\"]") +
            "[SetUp \"1\"]\n[FEN \"" +
            fen +
            "\"]\n\n37... Kf7 38. Rh7+ 1/2-1/2");
        var formatted = _codec.Format(game);

        Assert.Contains("[FEN \"" + fen + "\"]\n[SetUp \"1\"]", formatted);
        Assert.Contains("\n37... Kf7 38. Rh7+ 1/2-1/2\n\n", formatted);
        Assert.Equal(37, game.InitialState.FullmoveNumber);
    }

    [Fact]
    public void PreservesEscapedUnicodeAndSupplementalTagsInOrdinalOrder()
    {
        var pgn =
            Headers.Replace(
                "[Event \"Event\"]",
                "[Event \"Alice \\\"The Rook\\\" \\\\ Team\"]") +
            "[zeta \"z\"]\n[ECO \"C65\"]\n[Annotator \"José\"]\n\n*";
        var formatted = _codec.Format(_codec.Parse(pgn));

        Assert.Contains(
            "[Event \"Alice \\\"The Rook\\\" \\\\ Team\"]",
            formatted);
        Assert.True(
            formatted.IndexOf("[Annotator", StringComparison.Ordinal) <
            formatted.IndexOf("[ECO", StringComparison.Ordinal));
        Assert.True(
            formatted.IndexOf("[ECO", StringComparison.Ordinal) <
            formatted.IndexOf("[zeta", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1-0", PgnResult.WhiteWin)]
    [InlineData("0-1", PgnResult.BlackWin)]
    [InlineData("1/2-1/2", PgnResult.Draw)]
    [InlineData("*", PgnResult.Unknown)]
    public void MapsAllResultMarkers(
        string marker,
        PgnResult expected)
    {
        var parsed = _codec.Parse(
            Headers.Replace("*\"]", marker + "\"]") + "\n" + marker);
        Assert.Equal(expected, parsed.Result);
    }

    [Theory]
    [InlineData("1. e4 {comment} e5 *")]
    [InlineData("1. e4 ; comment\ne5 *")]
    [InlineData("1. e4 $1 e5 *")]
    [InlineData("1. e4 e5 (1... c5) *")]
    [InlineData("1. e4! e5 *")]
    [InlineData("%command\n1. e4 *")]
    [InlineData("1. e4 *\n[Event \"second\"]")]
    public void RejectsUnsupportedOrTrailingContent(
        string movetext)
    {
        Assert.Throws<FormatException>(() =>
            _codec.Parse(Headers + "\n" + movetext));
    }

    [Theory]
    [InlineData("2. e4 *")]
    [InlineData("2... e4 *")]
    [InlineData("1. e4 0-1")]
    [InlineData("1. e4")]
    public void RejectsInvalidMovetextMetadata(
        string movetext)
    {
        Assert.Throws<FormatException>(() =>
            _codec.Parse(Headers + "\n" + movetext));
    }

    [Theory]
    [InlineData("Event")]
    [InlineData("Site")]
    [InlineData("Date")]
    [InlineData("Round")]
    [InlineData("White")]
    [InlineData("Black")]
    [InlineData("Result")]
    public void RequiresEverySevenTagRosterTag(
        string tag)
    {
        var input = Headers.Replace(
            "[" + tag + " \"" + GetTagValue(tag) + "\"]\n",
            string.Empty);

        Assert.Throws<FormatException>(() => _codec.Parse(input + "\n*"));
    }

    [Fact]
    public void RejectsDuplicateTagsAndKeepsTagNamesCaseSensitive()
    {
        Assert.Throws<FormatException>(() => _codec.Parse(
            Headers + "[Event \"Duplicate\"]\n\n*"));

        var parsed = _codec.Parse(Headers + "[event \"supplemental\"]\n\n*");
        Assert.Equal("supplemental", parsed.AdditionalTags["event"]);
    }

    [Theory]
    [InlineData("[Event \"unterminated]")]
    [InlineData("[Event \"dangling\\\"]")]
    [InlineData("[Event \"invalid\\n\"]")]
    [InlineData("[Event \"line\nbreak\"]")]
    public void RejectsMalformedTagStrings(
        string eventTag)
    {
        Assert.Throws<FormatException>(() => _codec.Parse(
            Headers.Replace("[Event \"Event\"]", eventTag) + "\n*"));
    }

    [Fact]
    public void FormatsLegalMainlineThroughSanAndRejectsIllegalMoves()
    {
        var game = Variant.CreateGame();
        var e4 = TestSupport.FindMove(game, "e2", "e4");
        game.Execute(e4);
        var e5 = TestSupport.FindMove(game, "e7", "e5");
        var record = new PgnGame(
            "Event",
            "Site",
            "Date",
            "Round",
            "White",
            "Black",
            PgnResult.WhiteWin,
            StandardInitialState.Default,
            [e4, e5],
            []);
        var invalid = new PgnGame(
            "Event",
            "Site",
            "Date",
            "Round",
            "White",
            "Black",
            PgnResult.Unknown,
            StandardInitialState.Default,
            [new Move(TestSupport.Square("a1"), TestSupport.Square("a3"))],
            []);

        Assert.Contains("1. e4 e5 1-0", _codec.Format(record));
        Assert.Throws<ArgumentException>(() => _codec.Format(invalid));
    }

    [Fact]
    public void ComposesSanForCapturesAndCheckmate()
    {
        var capture = _codec.Parse(Headers + "\n1. e4 d5 2. exd5 *");
        var mate = _codec.Parse(Headers + "\n1. f3 e5 2. g4 Qh4# *");

        Assert.Contains("exd5", _codec.Format(capture));
        Assert.Contains("Qh4#", _codec.Format(mate));
    }

    [Fact]
    public void PreservesCustomFenRawEnPassantTarget()
    {
        const string fen = "4k3/8/8/3p4/8/8/8/4K3 w - d6 0 37";
        var pgn = Headers + "[SetUp \"1\"]\n[FEN \"" + fen + "\"]\n\n*";
        var parsed = _codec.Parse(pgn);

        Assert.Equal(
            TestSupport.Square("d6"),
            parsed.InitialState.EnPassantTarget);
        Assert.Contains("[FEN \"" + fen + "\"]", _codec.Format(parsed));
    }

    [Fact]
    public void WrapsLongMovetextAtTokenBoundaries()
    {
        var parsed = _codec.Parse(
            Headers +
            "\n1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 " +
            "4. Ba4 Nf6 5. O-O Be7 6. Re1 b5 7. Bb3 d6 " +
            "8. c3 O-O 9. h3 Nb8 10. d4 Nbd7 *");
        var formatted = _codec.Format(parsed);
        var movetext = formatted
            .Split("\n\n", StringSplitOptions.None)[1]
            .Split('\n');

        Assert.All(movetext, line => Assert.InRange(line.Length, 1, 79));
        Assert.Equal(
            parsed.Mainline,
            _codec.Parse(formatted)
                .Mainline);
        Assert.DoesNotContain(movetext, line => line.EndsWith(' '));
    }

    private const string Headers =
        "[Event \"Event\"]\n[Site \"Site\"]\n[Date \"2026.09.14\"]\n[Round \"1\"]\n[White \"White\"]\n[Black \"Black\"]\n[Result \"*\"]\n";

    private static string GetTagValue(
        string tag)
    {
        return tag switch
        {
            "Event" => "Event",
            "Site" => "Site",
            "Date" => "2026.09.14",
            "Round" => "1",
            "White" => "White",
            "Black" => "Black",
            "Result" => "*",
            _ => throw new ArgumentOutOfRangeException(nameof(tag))
        };
    }
}
