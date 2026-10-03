// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Stockfish;

internal static class StockfishCheckOption
{
    public static void Require(
        IReadOnlyList<string> optionLines,
        string name)
    {
        var matches = optionLines
            .Select(line => line.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries))
            .Where(tokens => tokens is ["option", "name", var optionName, ..] &&
                             optionName == name)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new StockfishException(
                $"Stockfish must advertise one valid {name} check option.");
        }

        var parts = matches[0];
        if (parts.Length != 7 ||
            parts[3] != "type" ||
            parts[4] != "check" ||
            parts[5] != "default" ||
            !bool.TryParse(parts[6], out _))
        {
            throw new StockfishException(
                $"Stockfish advertised a malformed {name} check option.");
        }
    }
}
