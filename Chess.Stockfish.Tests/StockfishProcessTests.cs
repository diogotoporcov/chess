// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Analysis;
using Chess.Variants.Standard;
using Chess.Variants.Standard.Notation.Uci;

namespace Chess.Stockfish.Tests;

public sealed class StockfishProcessTests
{
    private const string StartFen =
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public async Task StartupAppliesStandardModeAndValidatedOptions()
    {
        await using var host = new TestEngineHost();
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(threads: 4, hashSizeMiB: 64),
            TestContext.Current.CancellationToken);
        var lines = host.ReadLogLines();
        Assert.Contains("IN setoption name UCI_Chess960 value false", lines);
        Assert.Contains("IN setoption name Threads value 4", lines);
        Assert.Contains("IN setoption name Hash value 64", lines);
        Assert.Equal(3, lines.Count(line => line == "OUT readyok"));
    }

    [Theory]
    [InlineData(9, null)]
    [InlineData(null, 1025)]
    public async Task StartupRejectsOutOfRangeOptions(
        int? threads,
        int? hash)
    {
        await using var host = new TestEngineHost();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            StockfishAnalyzer.StartAsync(
                host.CreateOptions(threads, hash),
                TestContext.Current.CancellationToken));
        Assert.Contains("IN quit", host.ReadLogLines());
    }

    [Theory]
    [InlineData("stockfish no multipv")]
    [InlineData("stockfish malformed multipv")]
    public async Task MissingMultiPvCleansUpProcess(
        string scenario)
    {
        await using var host = new TestEngineHost(scenario);
        await Assert.ThrowsAsync<StockfishException>(() =>
            StockfishAnalyzer.StartAsync(
                host.CreateOptions(),
                TestContext.Current.CancellationToken));
        Assert.Contains("EVENT graceful-exit", host.ReadLogLines());
    }

    [Theory]
    [InlineData("stockfish no threads", true)]
    [InlineData("stockfish no hash", false)]
    public async Task ConfiguredOptionRequiresAdvertisedCapability(
        string scenario,
        bool configureThreads)
    {
        await using var host = new TestEngineHost(scenario);
        var options = configureThreads
            ? host.CreateOptions(threads: 2)
            : host.CreateOptions(hashSizeMiB: 32);
        await Assert.ThrowsAsync<StockfishException>(() =>
            StockfishAnalyzer.StartAsync(
                options,
                TestContext.Current.CancellationToken));
        Assert.Contains("EVENT graceful-exit", host.ReadLogLines());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task AcceptsAdvertisedMultiPvRange(
        int count)
    {
        await using var host = new TestEngineHost();
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(count),
            TestContext.Current.CancellationToken);
        Assert.Equal(count, result.Variations.Count);
        Assert.Contains(
            $"IN setoption name MultiPV value {count}",
            host.ReadLogLines());
    }

    [Fact]
    public async Task RejectsOutOfRangeMultiPvBeforeSearch()
    {
        await using var host = new TestEngineHost();
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(6),
                TestContext.Current.CancellationToken));
        Assert.DoesNotContain(
            host.ReadLogLines(),
            line => line.StartsWith("IN position", StringComparison.Ordinal) ||
                    line.StartsWith("IN go", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("depth", 20, "go depth 20")]
    [InlineData("nodes", 1000000, "go nodes 1000000")]
    [InlineData("time", 5000, "go movetime 5000")]
    public async Task MapsAnalysisLimits(
        string kind,
        int amount,
        string command)
    {
        await using var host = new TestEngineHost();
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var limit = kind switch
        {
            "depth" => AnalysisLimit.ByDepth(amount),
            "nodes" => AnalysisLimit.ByNodes(amount),
            _ => AnalysisLimit.ByTime(TimeSpan.FromMilliseconds(amount))
        };
        await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            new AnalysisRequest(limit),
            TestContext.Current.CancellationToken);
        Assert.Contains($"IN {command}", host.ReadLogLines());
    }

    [Fact]
    public async Task AggregatesLatestLinesInRankOrder()
    {
        await using var host = new TestEngineHost();
        host.SetResponse(
            "info depth 10 multipv 2 score cp 2 pv d2d4",
            "info depth 10 multipv 1 score cp 1 pv e2e4",
            "info depth 11 multipv 2 score cp 12 pv d2d4",
            "info depth 11 multipv 1 score cp 11 pv e2e4",
            "info depth 12 multipv 2 score cp 22 pv d2d4",
            "info depth 12 multipv 1 score cp 21 pv e2e4",
            "bestmove d2d4");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(2),
            TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Variations.Count);
        Assert.Equal(
            [21, 22],
            result.Variations.Select(variation => Assert
                .IsType<CentipawnScore>(variation.Score)
                .Centipawns));
        Assert.All(
            result.Variations,
            variation => Assert.Equal(12, variation.Depth));
        Assert.Equal(
            "d2d4",
            new UciMoveCodec().Format(
                Variant.CreateGame(),
                result.BestMove!.Value));
    }

    [Fact]
    public async Task FewerVariationsThanRequestedIsValid()
    {
        await using var host = new TestEngineHost();
        host.SetResponse(
            "info multipv 1 score cp 1 pv e2e4",
            "info multipv 2 score cp 2 pv d2d4",
            "bestmove e2e4");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(5),
            TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Variations.Count);
    }

    [Fact]
    public async Task ConcurrentAnalysesKeepMultiPvAndSearchTogether()
    {
        await using var host = new TestEngineHost("stockfish held first");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var facts = PositionFacts.FromFen(StartFen);
        var first = analyzer.AnalyzeAsync(
            facts,
            Request(2),
            TestContext.Current.CancellationToken);
        await host.WaitForLogLineAsync("EVENT first-search-held");
        var second = analyzer.AnalyzeAsync(
            facts,
            Request(3),
            TestContext.Current.CancellationToken);
        Assert.False(second.IsCompleted);
        Assert.DoesNotContain(
            "IN setoption name MultiPV value 3",
            host.ReadLogLines());
        await File.WriteAllTextAsync(
            host.ReleasePath,
            string.Empty,
            TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(first, second);
        Assert.Equal(2, results[0].Variations.Count);
        Assert.Equal(3, results[1].Variations.Count);
        var lines = host.ReadLogLines();
        Assert.True(
            Array.IndexOf(lines, "OUT bestmove e2e4") <
            Array.IndexOf(lines, "IN setoption name MultiPV value 3"));
    }

    [Fact]
    public async Task QueuedCancellationWritesNothing()
    {
        await using var host = new TestEngineHost("stockfish held first");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var facts = PositionFacts.FromFen(StartFen);
        var first = analyzer.AnalyzeAsync(
            facts,
            Request(2),
            TestContext.Current.CancellationToken);
        await host.WaitForLogLineAsync("EVENT first-search-held");
        using var canceled = new CancellationTokenSource();
        var second = analyzer.AnalyzeAsync(facts, Request(3), canceled.Token);
        await canceled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        await File.WriteAllTextAsync(
            host.ReleasePath,
            string.Empty,
            TestContext.Current.CancellationToken);
        await first;
        var lines = host.ReadLogLines();
        Assert.DoesNotContain("IN setoption name MultiPV value 3", lines);
        Assert.Single(lines, line => line == "IN go depth 10");
    }

    [Fact]
    public async Task ActiveCancellationDrainsAndAllowsLaterAnalysis()
    {
        await using var host = new TestEngineHost("stockfish wait for stop");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        using var canceled = new CancellationTokenSource();
        var first = analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            canceled.Token);
        await host.WaitForLogLineAsync("IN go depth 10");
        await canceled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        var second = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            TestContext.Current.CancellationToken);
        Assert.NotNull(second.BestMove);
        var lines = host.ReadLogLines();
        Assert.Contains("IN stop", lines);
        Assert.True(
            Array.IndexOf(lines, "OUT bestmove e2e4") <
            Array.LastIndexOf(lines, $"IN position fen {StartFen}"));
    }

    [Fact]
    public async Task DisposalRejectsLaterAnalysis()
    {
        await using var host = new TestEngineHost();
        var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        await analyzer.DisposeAsync();
        await analyzer.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                TestContext.Current.CancellationToken));
        Assert.Contains("EVENT graceful-exit", host.ReadLogLines());
    }

    private static AnalysisRequest Request(
        int variationCount = 1)
    {
        return new AnalysisRequest(AnalysisLimit.ByDepth(10), variationCount);
    }
}
