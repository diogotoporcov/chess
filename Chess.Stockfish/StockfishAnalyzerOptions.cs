// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Uci;

namespace Chess.Stockfish;

public sealed record StockfishAnalyzerOptions
{
    public UciEngineProcessOptions ProcessOptions { get; }

    public int? Threads { get; }

    public int? HashSizeMiB { get; }

    public int? StrengthElo { get; }

    public StockfishAnalyzerOptions(
        UciEngineProcessOptions processOptions,
        int? threads = null,
        int? hashSizeMiB = null,
        int? strengthElo = null)
    {
        ArgumentNullException.ThrowIfNull(processOptions);
        if (threads is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threads));
        }

        if (hashSizeMiB is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hashSizeMiB));
        }

        ProcessOptions = processOptions;
        Threads = threads;
        HashSizeMiB = hashSizeMiB;
        StrengthElo = strengthElo;
    }
}
