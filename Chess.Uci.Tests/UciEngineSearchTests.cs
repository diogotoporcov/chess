// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Uci.Tests;

public sealed class UciEngineSearchTests
{
    [Fact]
    public async Task SearchAsync_SerializesStartPositionAndPreservesResult()
    {
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();

        var result = await engine.SearchAsync(
            UciPosition.FromStartPosition(["e2e4", "e7e5"]),
            UciGoCommand.ByDepth(20),
            TestContext.Current.CancellationToken);

        Assert.Equal("e2e4", result.BestMove);
        Assert.Equal("e7e5", result.PonderMove);
        Assert.Equal(
            [
                "info depth 1 nodes 20 pv e2e4",
                "info string deterministic result"
            ],
            result.InfoLines);
        Assert.Contains(
            "IN position startpos moves e2e4 e7e5",
            host.ReadLogLines());
        Assert.Contains("IN go depth 20", host.ReadLogLines());
    }

    [Fact]
    public async Task SearchAsync_SerializesFenWithoutParsingIt()
    {
        const string fen = "8/8/8/8/8/8/8/K6k w - - 0 1";
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();

        await engine.SearchAsync(
            UciPosition.FromFen(fen),
            UciGoCommand.ByNodes(1_000_000),
            TestContext.Current.CancellationToken);
        await engine.SearchAsync(
            UciPosition.FromFen(fen, ["a1a2"]),
            UciGoCommand.ByNodes(1),
            TestContext.Current.CancellationToken);

        Assert.Contains($"IN position fen {fen}", host.ReadLogLines());
        Assert.Contains(
            $"IN position fen {fen} moves a1a2",
            host.ReadLogLines());
        Assert.Contains("IN go nodes 1000000", host.ReadLogLines());
    }

    [Fact]
    public async Task SearchAsync_SerializesAllSupportedGoLimitsExactly()
    {
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();
        var position = UciPosition.FromStartPosition();

        await engine.SearchAsync(
            position,
            UciGoCommand.ByDepth(1),
            TestContext.Current.CancellationToken);
        await engine.SearchAsync(
            position,
            UciGoCommand.ByDepth(20),
            TestContext.Current.CancellationToken);
        await engine.SearchAsync(
            position,
            UciGoCommand.ByNodes(1),
            TestContext.Current.CancellationToken);
        await engine.SearchAsync(
            position,
            UciGoCommand.ByNodes(1_000_000),
            TestContext.Current.CancellationToken);
        await engine.SearchAsync(
            position,
            UciGoCommand.ByMoveTime(TimeSpan.FromMilliseconds(1)),
            TestContext.Current.CancellationToken);
        await engine.SearchAsync(
            position,
            UciGoCommand.ByMoveTime(TimeSpan.FromSeconds(5)),
            TestContext.Current.CancellationToken);
        await engine.SearchAsync(
            position,
            UciGoCommand.ByMoveTime(TimeSpan.FromTicks(1)),
            TestContext.Current.CancellationToken);

        var goLines = host
            .ReadLogLines()
            .Where(line => line.StartsWith("IN go ", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            [
                "IN go depth 1", "IN go depth 20", "IN go nodes 1",
                "IN go nodes 1000000", "IN go movetime 1",
                "IN go movetime 5000",
                "IN go movetime 1"
            ],
            goLines);
        Assert.Contains("IN position startpos", host.ReadLogLines());
    }

    [Theory]
    [InlineData("sentinel zero", "0000")]
    [InlineData("sentinel none", "(none)")]
    public async Task SearchAsync_PreservesBestMoveSentinels(
        string scenario,
        string expectedMove)
    {
        await using var host = new TestEngineHost(scenario);
        await using var engine = await host.StartAsync();

        var result = await engine.SearchAsync(
            UciPosition.FromStartPosition(),
            UciGoCommand.ByDepth(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedMove, result.BestMove);
        Assert.Null(result.PonderMove);
    }

    [Theory]
    [InlineData("malformed empty")]
    [InlineData("malformed ponder")]
    [InlineData("malformed trailing")]
    public async Task SearchAsync_RejectsMalformedBestMove(
        string scenario)
    {
        await using var host = new TestEngineHost(scenario);
        await using var engine = await host.StartAsync();

        await Assert.ThrowsAsync<UciEngineException>(() => engine.SearchAsync(
            UciPosition.FromStartPosition(),
            UciGoCommand.ByDepth(1),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_ReportsUnexpectedProcessExit()
    {
        await using var host = new TestEngineHost("exit on search");
        await using var engine = await host.StartAsync();

        var exception = await Assert.ThrowsAsync<UciEngineException>(() =>
            engine.SearchAsync(
                UciPosition.FromStartPosition(),
                UciGoCommand.ByDepth(1),
                TestContext.Current.CancellationToken));

        Assert.Equal(17, exception.ExitCode);
        Assert.Contains("fatal-search-message", exception.StandardErrorLines);
    }

    [Fact]
    public async Task StopAsync_IsIdempotentAndSearchCompletesNormally()
    {
        await using var host = new TestEngineHost("wait for stop");
        await using var engine = await host.StartAsync();
        var search = engine.SearchAsync(
            UciPosition.FromStartPosition(),
            UciGoCommand.ByDepth(10),
            TestContext.Current.CancellationToken);
        await host.WaitForLogLineAsync("IN go depth 10");

        await Task.WhenAll(
            engine.StopAsync(TestContext.Current.CancellationToken),
            engine.StopAsync(TestContext.Current.CancellationToken),
            engine.StopAsync(TestContext.Current.CancellationToken));
        var result = await search;

        Assert.Equal("a2a3", result.BestMove);
        Assert.Equal(
            1,
            host
                .ReadLogLines()
                .Count(line => line == "IN stop"));
    }

    [Fact]
    public async Task StopAsync_WithNoActiveSearchIsNoOp()
    {
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();

        await engine.StopAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("IN stop", host.ReadLogLines());
    }

    [Fact]
    public async Task QuitAsync_StopsActiveSearchBeforeQuitting()
    {
        await using var host = new TestEngineHost("wait for stop");
        var engine = await host.StartAsync();
        var search = engine.SearchAsync(
            UciPosition.FromStartPosition(),
            UciGoCommand.ByDepth(10),
            TestContext.Current.CancellationToken);
        await host.WaitForLogLineAsync("IN go depth 10");

        var quit = engine.QuitAsync(TestContext.Current.CancellationToken);
        var result = await search;
        await quit;

        Assert.Equal("a2a3", result.BestMove);
        Assert.Equal(
            1,
            host
                .ReadLogLines()
                .Count(line => line == "IN stop"));
        Assert.Equal(
            1,
            host
                .ReadLogLines()
                .Count(line => line == "IN quit"));
    }

    [Fact]
    public async Task QuitAsync_DoesNotMissSearchDuringInitialPositionWrite()
    {
        await using var host = new TestEngineHost("pause search input");
        var engine = await host.StartAsync(
            shutdownTimeout: TimeSpan.FromSeconds(5));
        await host.WaitForLogLineAsync("EVENT search-input-paused");
        var search = engine.SearchAsync(
            UciPosition.FromFen(new string('x', 4 * 1024 * 1024)),
            UciGoCommand.ByDepth(10),
            TestContext.Current.CancellationToken);
        Assert.False(search.IsCompleted);

        var quit = engine.QuitAsync(TestContext.Current.CancellationToken);
        Assert.False(quit.IsCompleted);
        await File.WriteAllTextAsync(
            host.ReleasePath,
            string.Empty,
            TestContext.Current.CancellationToken);

        var result = await search;
        await quit;

        Assert.Equal("a2a3", result.BestMove);
        var lines = host.ReadLogLines();
        Assert.Equal(
            [
                "IN uci", "IN position startup-race", "IN go depth 10",
                "IN stop",
                "IN quit"
            ],
            lines
                .Where(line => line.StartsWith("IN ", StringComparison.Ordinal))
                .ToArray());
        Assert.True(
            Array.IndexOf(lines, "IN stop") <
            Array.IndexOf(lines, "OUT bestmove a2a3"));
        Assert.True(
            Array.IndexOf(lines, "OUT bestmove a2a3") <
            Array.IndexOf(lines, "IN quit"));
        Assert.True(
            Array.IndexOf(lines, "IN quit") <
            Array.IndexOf(lines, "EVENT graceful-exit"));
    }

    [Fact]
    public async Task SearchCancellation_DrainsBestMoveBeforeNextSearch()
    {
        await using var host = new TestEngineHost("wait for stop");
        await using var engine = await host.StartAsync();
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
        var firstSearch = engine.SearchAsync(
            UciPosition.FromStartPosition(["a2a3"]),
            UciGoCommand.ByDepth(10),
            cancellation.Token);
        await host.WaitForLogLineAsync("IN go depth 10");

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            firstSearch);
        var secondResult = await engine.SearchAsync(
            UciPosition.FromStartPosition(["h2h3"]),
            UciGoCommand.ByDepth(1),
            TestContext.Current.CancellationToken);

        Assert.Equal("h2h3", secondResult.BestMove);
        Assert.Equal(
            1,
            host
                .ReadLogLines()
                .Count(line => line == "IN stop"));
        var lines = host.ReadLogLines();
        Assert.True(
            Array.IndexOf(lines, "OUT bestmove a2a3") <
            Array.IndexOf(lines, "IN position startpos moves h2h3"));
    }

    [Fact]
    public async Task SearchCancellation_TimeoutFaultsEngine()
    {
        await using var host = new TestEngineHost("ignore stop");
        await using var engine = await host.StartAsync(
            responseTimeout: TimeSpan.FromSeconds(2));
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
        var search = engine.SearchAsync(
            UciPosition.FromStartPosition(),
            UciGoCommand.ByDepth(10),
            cancellation.Token);
        await host.WaitForLogLineAsync("IN go depth 10");

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => search);
        await Assert.ThrowsAsync<UciEngineException>(() => engine.SearchAsync(
            UciPosition.FromStartPosition(),
            UciGoCommand.ByDepth(1),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_PreCanceledTokenWritesNothing()
    {
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var before = host.ReadLogLines();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.SearchAsync(
                UciPosition.FromStartPosition(),
                UciGoCommand.ByDepth(1),
                cancellation.Token));

        Assert.Equal(before, host.ReadLogLines());
    }

    [Fact]
    public async Task SearchAsync_SerializesConcurrentSearches()
    {
        await using var host = new TestEngineHost("serial searches");
        await using var engine = await host.StartAsync();

        var first = engine.SearchAsync(
            UciPosition.FromStartPosition(["a2a3"]),
            UciGoCommand.ByDepth(1),
            TestContext.Current.CancellationToken);
        var second = engine.SearchAsync(
            UciPosition.FromStartPosition(["h2h3"]),
            UciGoCommand.ByDepth(2),
            TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(first, second);

        Assert.Equal("a2a3", results[0].BestMove);
        Assert.Equal("h2h3", results[1].BestMove);
        var lines = host.ReadLogLines();
        Assert.True(
            Array.IndexOf(lines, "OUT bestmove a2a3") <
            Array.IndexOf(lines, "IN position startpos moves h2h3"));
    }

    [Fact]
    public void UciSearchResult_DefensivelyCopiesInfoLines()
    {
        var lines = new List<string> { "info depth 1" };
        var result = new UciSearchResult("e2e4", null, lines);

        lines[0] = "changed";

        Assert.Equal(["info depth 1"], result.InfoLines);
    }
}
