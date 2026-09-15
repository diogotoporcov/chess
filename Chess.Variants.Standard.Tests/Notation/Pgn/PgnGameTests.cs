// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Movement;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Notation.Pgn;

namespace Chess.Variants.Standard.Tests.Notation.Pgn;

public sealed class PgnGameTests
{
    [Fact]
    public void DefensivelyCopiesCollections()
    {
        var moves = new List<Move>();
        var tags = new List<KeyValuePair<string, string>>
        {
            new("Annotator", "A")
        };

        var game = CreateGame(moves, tags);
        moves.Add(new Move(TestSupport.Square("e2"), TestSupport.Square("e4")));
        tags[0] = new KeyValuePair<string, string>("Annotator", "B");

        Assert.Empty(game.Mainline);
        Assert.Equal("A", game.AdditionalTags["Annotator"]);
        Assert.Throws<NotSupportedException>(() =>
            ((ICollection<Move>)game.Mainline).Add(default));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string>)game.AdditionalTags).Add(
                "ECO",
                "C65"));
    }

    [Theory]
    [InlineData("Event")]
    [InlineData("Site")]
    [InlineData("Date")]
    [InlineData("Round")]
    [InlineData("White")]
    [InlineData("Black")]
    [InlineData("Result")]
    [InlineData("SetUp")]
    [InlineData("FEN")]
    public void RejectsReservedSupplementalTags(
        string name)
    {
        Assert.Throws<ArgumentException>(() => CreateGame(
            [],
            [new KeyValuePair<string, string>(name, "value")]));
    }

    [Theory]
    [InlineData("bad-name")]
    [InlineData("")]
    [InlineData("bad name")]
    public void RejectsInvalidSupplementalTagNames(
        string name)
    {
        Assert.Throws<ArgumentException>(() => CreateGame(
            [],
            [new KeyValuePair<string, string>(name, "value")]));
    }

    private static PgnGame CreateGame(
        IEnumerable<Move> mainline,
        IEnumerable<KeyValuePair<string, string>> tags)
    {
        return new PgnGame(
            "Event",
            "Site",
            "2026.09.14",
            "1",
            "White",
            "Black",
            PgnResult.Unknown,
            StandardInitialState.Default,
            mainline,
            tags);
    }
}
