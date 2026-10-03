// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace Chess.Stockfish;

internal readonly record struct StockfishSpinOption(int Minimum, int Maximum)
{
    public static StockfishSpinOption Require(
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
                $"Stockfish must advertise one valid {name} spin option.");
        }

        var parts = matches[0];
        var type = Array.IndexOf(parts, "type");
        var min = Array.IndexOf(parts, "min");
        var max = Array.IndexOf(parts, "max");
        if (type < 0 ||
            type + 1 >= parts.Length ||
            parts[type + 1] != "spin" ||
            min < 0 ||
            min + 1 >= parts.Length ||
            max < 0 ||
            max + 1 >= parts.Length ||
            !int.TryParse(
                parts[min + 1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var minimum) ||
            !int.TryParse(
                parts[max + 1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var maximum) ||
            minimum < 1 ||
            minimum > maximum)
        {
            throw new StockfishException(
                $"Stockfish advertised a malformed {name} spin option.");
        }

        return new StockfishSpinOption(minimum, maximum);
    }

    public void Validate(
        int value,
        string parameterName)
    {
        if (value < Minimum ||
            value > Maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Value must be within the advertised range {Minimum}..{Maximum}.");
        }
    }
}
