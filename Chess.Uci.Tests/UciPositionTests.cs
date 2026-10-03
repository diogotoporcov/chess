// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Uci.Tests;

public sealed class UciPositionTests
{
    [Fact]
    public void FromStartPosition_PreservesMoves()
    {
        var withoutMoves = UciPosition.FromStartPosition();
        var withMoves = UciPosition.FromStartPosition(["e2e4", "e7e5"]);

        Assert.True(withoutMoves.IsStartPosition);
        Assert.Null(withoutMoves.Fen);
        Assert.Empty(withoutMoves.Moves);
        Assert.Equal(["e2e4", "e7e5"], withMoves.Moves);
    }

    [Fact]
    public void FromFen_PreservesRawFenAndMoves()
    {
        const string fen = "8/8/8/8/8/8/8/K6k w - - 0 1";

        var withoutMoves = UciPosition.FromFen(fen);
        var withMoves = UciPosition.FromFen(fen, ["a1a2"]);

        Assert.False(withoutMoves.IsStartPosition);
        Assert.Equal(fen, withoutMoves.Fen);
        Assert.Empty(withoutMoves.Moves);
        Assert.Equal(["a1a2"], withMoves.Moves);
    }

    [Fact]
    public void Factory_DefensivelyCopiesMoves()
    {
        var moves = new List<string> { "e2e4" };
        var position = UciPosition.FromStartPosition(moves);

        moves[0] = "d2d4";
        moves.Add("d7d5");

        Assert.Equal(["e2e4"], position.Moves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromFen_RejectsInvalidFen(
        string? fen)
    {
        Assert.ThrowsAny<ArgumentException>(() => UciPosition.FromFen(fen!));
    }

    [Theory]
    [InlineData("fen\rvalue")]
    [InlineData("fen\nvalue")]
    [InlineData("fen\0value")]
    public void FromFen_RejectsCommandInjection(
        string fen)
    {
        Assert.Throws<ArgumentException>(() => UciPosition.FromFen(fen));
    }

    [Fact]
    public void Factory_RejectsNullMove()
    {
        Assert.Throws<ArgumentException>(() =>
            UciPosition.FromStartPosition([null!]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("e2 e4")]
    [InlineData("e2\te4")]
    [InlineData("e2\re4")]
    [InlineData("e2\ne4")]
    [InlineData("e2\0e4")]
    public void Factory_RejectsInvalidMoveToken(
        string move)
    {
        Assert.Throws<ArgumentException>(() =>
            UciPosition.FromStartPosition([move]));
    }

    [Fact]
    public void Factory_PreservesVariantMoveTokenExactly()
    {
        var position = UciPosition.FromStartPosition(["P@e4"]);

        Assert.Equal("P@e4", Assert.Single(position.Moves));
    }
}
