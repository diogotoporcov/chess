// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Stockfish.Tests;

public sealed class StockfishStrengthTests
{
    [Fact]
    public async Task NullStrengthLeavesLimitDisabled()
    {
        await using var host = new TestEngineHost();
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            host.ReadLogLines(),
            line => line.Contains("UCI_Elo") &&
                    line.StartsWith("IN setoption"));
        Assert.DoesNotContain(
            host.ReadLogLines(),
            line => line.Contains("UCI_LimitStrength") &&
                    line.StartsWith("IN setoption"));
    }

    [Fact]
    public async Task LimitedStrengthSetsEloBeforeEnablingLimit()
    {
        await using var host = new TestEngineHost();
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(strengthElo: 1800),
            TestContext.Current.CancellationToken);
        var lines = host.ReadLogLines();
        var elo = Array.IndexOf(lines, "IN setoption name UCI_Elo value 1800");
        var limit = Array.IndexOf(
            lines,
            "IN setoption name UCI_LimitStrength value true");
        Assert.True(elo >= 0);
        Assert.True(limit > elo);
    }

    [Theory]
    [InlineData("stockfish no limitstrength", 1800)]
    [InlineData("stockfish malformed limitstrength", 1800)]
    [InlineData("stockfish no elo", 1800)]
    [InlineData("stockfish malformed elo", 1800)]
    [InlineData("stockfish normal", 1319)]
    [InlineData("stockfish normal", 3191)]
    public async Task RejectsUnsupportedStrengthAndDisposesProcess(
        string scenario,
        int elo)
    {
        await using var host = new TestEngineHost(scenario);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            StockfishAnalyzer.StartAsync(
                host.CreateOptions(strengthElo: elo),
                TestContext.Current.CancellationToken));
        Assert.Contains("IN quit", host.ReadLogLines());
    }
}
