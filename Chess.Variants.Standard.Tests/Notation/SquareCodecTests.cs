// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Board;
using Chess.Core.Notation;
using Chess.Variants.Standard.Board;
using Chess.Variants.Standard.Notation;

namespace Chess.Variants.Standard.Tests.Notation;

public sealed class SquareCodecTests
{
    private readonly SquareCodec _codec = new();

    [Fact]
    public void AllStandardSquaresRoundTrip()
    {
        for (var row = 0; row < BoardLayout.Rows; row++)
        {
            for (var column = 0; column < BoardLayout.Columns; column++)
            {
                var square = BoardLayout.GetSquare(row, column);
                Assert.Equal(square, _codec.Parse(_codec.Format(square)));
            }
        }
    }

    [Fact]
    public void ImplementsGenericNotationCodec()
    {
        INotationCodec<Square> codec = new SquareCodec();

        Assert.Equal("e4", codec.Format(codec.Parse("e4")));
    }

    [Theory]
    [InlineData("A1")]
    [InlineData("i1")]
    [InlineData("a0")]
    [InlineData("a9")]
    [InlineData("a")]
    [InlineData("a10")]
    public void ParseRejectsNonCanonicalCoordinates(
        string notation)
    {
        Assert.Throws<FormatException>(() => _codec.Parse(notation));
    }

    [Fact]
    public void ParseRejectsNullAndFormatRejectsOutsideBoard()
    {
        Assert.Throws<ArgumentNullException>(() => _codec.Parse(null!));
        Assert.Throws<ArgumentException>(() => _codec.Format(new Square(64)));
    }
}
