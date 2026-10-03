// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;

namespace Chess.Uci;

public sealed record UciSearchResult
{
    public string BestMove { get; }

    public string? PonderMove { get; }

    public IReadOnlyList<string> InfoLines { get; }

    public UciSearchResult(
        string bestMove,
        string? ponderMove,
        IEnumerable<string> infoLines)
    {
        ArgumentException.ThrowIfNullOrEmpty(bestMove);
        ArgumentNullException.ThrowIfNull(infoLines);

        var nullableLines = infoLines
            .Cast<string?>()
            .ToArray();
        if (nullableLines.Any(line => line is null))
        {
            throw new ArgumentException(
                "Info lines must not contain null entries.",
                nameof(infoLines));
        }

        var lines = nullableLines
            .Select(line => line!)
            .ToArray();

        BestMove = bestMove;
        PonderMove = ponderMove;
        InfoLines = new ReadOnlyCollection<string>(lines);
    }
}
