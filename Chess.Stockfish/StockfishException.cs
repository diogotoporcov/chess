// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Stockfish;

public sealed class StockfishException : Exception
{
    public StockfishException(
        string message,
        Exception? innerException = null) : base(message, innerException)
    {
    }
}
