// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;

namespace Chess.Uci;

public sealed record UciPosition
{
    private readonly string? _fen;

    public bool IsStartPosition => _fen is null;

    public string? Fen => _fen;

    public IReadOnlyList<string> Moves { get; }

    private UciPosition(
        string? fen,
        IEnumerable<string>? moves)
    {
        _fen = fen;
        Moves = CopyMoves(moves);
    }

    public static UciPosition FromStartPosition(
        IEnumerable<string>? moves = null)
    {
        return new UciPosition(null, moves);
    }

    public static UciPosition FromFen(
        string fen,
        IEnumerable<string>? moves = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);
        UciProtocolText.ValidateSingleLine(fen, nameof(fen));

        return new UciPosition(fen, moves);
    }

    internal string ToProtocolCommand()
    {
        var command = IsStartPosition
            ? "position startpos"
            : $"position fen {_fen}";

        return Moves.Count == 0
            ? command
            : $"{command} moves {string.Join(' ', Moves)}";
    }

    private static IReadOnlyList<string> CopyMoves(
        IEnumerable<string>? moves)
    {
        var moveArray = moves
            ?.Cast<string?>()
            .ToArray() ?? [];
        for (var index = 0; index < moveArray.Length; index++)
        {
            var move = moveArray[index];
            if (move is null)
            {
                throw new ArgumentException(
                    "Moves must not contain null entries.",
                    nameof(moves));
            }

            if (move.Length == 0)
            {
                throw new ArgumentException(
                    "Move tokens must not be empty.",
                    nameof(moves));
            }

            if (move.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException(
                    "Move tokens must not contain whitespace.",
                    nameof(moves));
            }

            UciProtocolText.ValidateSingleLine(move, nameof(moves));
        }

        return new ReadOnlyCollection<string>(
            moveArray
                .Select(move => move!)
                .ToArray());
    }
}
