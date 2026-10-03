// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;

namespace Chess.Uci;

public sealed record UciEngineInfo
{
    public string? Name { get; }

    public string? Author { get; }

    public IReadOnlyList<string> OptionLines { get; }

    public UciEngineInfo(
        string? name,
        string? author,
        IEnumerable<string> optionLines)
    {
        ArgumentNullException.ThrowIfNull(optionLines);

        var nullableLines = optionLines
            .Cast<string?>()
            .ToArray();
        if (nullableLines.Any(line => line is null))
        {
            throw new ArgumentException(
                "Option lines must not contain null entries.",
                nameof(optionLines));
        }

        var lines = nullableLines
            .Select(line => line!)
            .ToArray();

        Name = name;
        Author = author;
        OptionLines = new ReadOnlyCollection<string>(lines);
    }
}
