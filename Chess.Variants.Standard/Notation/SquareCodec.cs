// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Board;
using Chess.Core.Notation;
using Chess.Variants.Standard.Board;

namespace Chess.Variants.Standard.Notation;

public sealed class SquareCodec : INotationCodec<Square>
{
    public Square Parse(
        string notation)
    {
        ArgumentNullException.ThrowIfNull(notation);

        if (notation.Length != 2 ||
            notation[0] is < 'a' or > 'h' ||
            notation[1] is < '1' or > '8')
        {
            throw new FormatException(
                "Standard square notation must be a lowercase coordinate " +
                "from a1 through h8.");
        }

        return BoardLayout.GetSquare('8' - notation[1], notation[0] - 'a');
    }

    public string Format(
        Square value)
    {
        try
        {
            var row = BoardLayout.GetRow(value);
            var column = BoardLayout.GetColumn(value);

            return string.Create(
                2,
                (row, column),
                static (characters, position) =>
                {
                    characters[0] = (char)('a' + position.column);
                    characters[1] = (char)('8' - position.row);
                });
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ArgumentException(
                "Square is not part of the standard chess board.",
                nameof(value),
                exception);
        }
    }
}
