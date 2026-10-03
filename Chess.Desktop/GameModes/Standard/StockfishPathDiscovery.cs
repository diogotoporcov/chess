// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;

namespace Chess.Desktop.GameModes.Standard;

public static class StockfishPathDiscovery
{
    public static string Discover()
    {
        var configured = Environment.GetEnvironmentVariable(
            "CHESS_STOCKFISH_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var local = Path.Combine(AppContext.BaseDirectory, "stockfish.exe");
        return File.Exists(local) ? local : string.Empty;
    }
}
