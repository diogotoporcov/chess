// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Stockfish.Tests;

public sealed class StockfishAnalyzerOptionsTests
{
    [Fact]
    public void RequiresProcessOptions()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StockfishAnalyzerOptions(null!));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(null, 0)]
    [InlineData(null, -1)]
    public async Task RequiresPositiveConfiguredValues(
        int? threads,
        int? hash)
    {
        await using var host = new TestEngineHost();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            host.CreateOptions(threads, hash));
    }
}
