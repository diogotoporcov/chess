// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Analysis;
using Chess.Uci;
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
    [InlineData("stockfish no chess960")]
    [InlineData("stockfish duplicate chess960")]
    [InlineData("stockfish wrong chess960")]
    [InlineData("stockfish malformed chess960")]
    public async Task InvalidChess960CapabilityCleansUpProcess(
        string scenario)
    {
        await using var host = new TestEngineHost(scenario);
        await Assert.ThrowsAsync<StockfishException>(() =>
            StockfishAnalyzer.StartAsync(
                host.CreateOptions(),
                TestContext.Current.CancellationToken));
        Assert.Contains("EVENT graceful-exit", host.ReadLogLines());
        Assert.DoesNotContain(
            "IN setoption name UCI_Chess960 value false",
            host.ReadLogLines());
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
    public async Task LatestRanksMayHaveDifferentMetadata()
    {
        await using var host = new TestEngineHost();
        host.SetResponse(
            "info depth 12 seldepth 20 multipv 1 score cp 10 nodes 100 time 5 pv e2e4",
            "info depth 11 multipv 2 score cp 5 nodes 80 time 4 pv d2d4",
            "bestmove e2e4");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(2),
            TestContext.Current.CancellationToken);
        Assert.Equal(12, result.Variations[0].Depth);
        Assert.Equal(20, result.Variations[0].SelectiveDepth);
        Assert.Equal(11, result.Variations[1].Depth);
        Assert.Null(result.Variations[1].SelectiveDepth);
        Assert.Equal(100, result.Variations[0].Nodes);
        Assert.Equal(80, result.Variations[1].Nodes);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(1, 2, 4)]
    public async Task RejectsNonContiguousMultiPvRanks(
        params int[] ranks)
    {
        await using var host = new TestEngineHost();
        host.SetResponse(
        [
            .. ranks.Select(rank =>
                $"info multipv {rank} score cp {rank} pv e2e4"),
            "bestmove e2e4"
        ]);
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<StockfishException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(5),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsMultiPvRankAboveRequest()
    {
        await using var host = new TestEngineHost();
        host.SetResponse(
            "info multipv 1 score cp 1 pv e2e4",
            "info multipv 2 score cp 2 pv d2d4",
            "info multipv 3 score cp 3 pv c2c4",
            "bestmove e2e4");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<StockfishException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(2),
                TestContext.Current.CancellationToken));
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
        Assert.Equal(2, lines.Count(line => line == "IN isready"));
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
        var linesBeforeRejectedAnalysis = host.ReadLogLines();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                TestContext.Current.CancellationToken));
        Assert.Equal(linesBeforeRejectedAnalysis, host.ReadLogLines());
        Assert.Contains("EVENT graceful-exit", linesBeforeRejectedAnalysis);
    }

    [Fact]
    public async Task ConcurrentDisposeCallsShareCleanupCompletion()
    {
        await using var host = new TestEngineHost("stockfish held quit");
        var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(shutdownTimeout: TimeSpan.FromSeconds(5)),
            TestContext.Current.CancellationToken);
        var first = analyzer
            .DisposeAsync()
            .AsTask();
        await host.WaitForLogLineAsync("EVENT shutdown-held");
        var second = analyzer
            .DisposeAsync()
            .AsTask();
        Assert.False(second.IsCompleted);
        await File.WriteAllTextAsync(
            host.ReleasePath,
            string.Empty,
            TestContext.Current.CancellationToken);
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task DisposeCancelsActiveAnalysisBeforeQuit()
    {
        await using var host = new TestEngineHost("stockfish wait for stop");
        var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var analysis = analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            TestContext.Current.CancellationToken);
        await host.WaitForLogLineAsync("IN go depth 10");
        var dispose = analyzer
            .DisposeAsync()
            .AsTask();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => analysis);
        await dispose;
        var lines = host.ReadLogLines();
        Assert.Single(lines, line => line == "IN stop");
        Assert.True(
            Array.IndexOf(lines, "IN go depth 10") <
            Array.IndexOf(lines, "IN stop"));
        Assert.True(
            Array.IndexOf(lines, "IN stop") <
            Array.IndexOf(lines, "OUT bestmove e2e4"));
        Assert.True(
            Array.IndexOf(lines, "OUT bestmove e2e4") <
            Array.IndexOf(lines, "IN quit"));
        Assert.Contains("EVENT graceful-exit", lines);
    }

    [Fact]
    public async Task DisposeCancelsQueuedAnalysisWithoutWritingItsCommands()
    {
        await using var host = new TestEngineHost("stockfish wait for stop");
        var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var first = analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(2),
            TestContext.Current.CancellationToken);
        await host.WaitForLogLineAsync("IN go depth 10");
        var second = analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(3),
            TestContext.Current.CancellationToken);
        var dispose = analyzer
            .DisposeAsync()
            .AsTask();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => first);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => second);
        await dispose;
        var lines = host.ReadLogLines();
        Assert.DoesNotContain("IN setoption name MultiPV value 3", lines);
        Assert.Single(
            lines,
            line => line.StartsWith("IN position ", StringComparison.Ordinal));
        Assert.Single(lines, line => line == "IN go depth 10");
    }

    [Fact]
    public async Task CallerCancellationBeforeDisposalRemainsCancellation()
    {
        await using var host = new TestEngineHost("stockfish wait for stop");
        var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var analysis = analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            cancellation.Token);
        await host.WaitForLogLineAsync("IN go depth 10");
        await cancellation.CancelAsync();
        await host.WaitForLogLineAsync("IN stop");
        var dispose = analyzer
            .DisposeAsync()
            .AsTask();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => analysis);
        await dispose;
        var lines = host.ReadLogLines();
        Assert.Single(lines, line => line == "IN stop");
        Assert.Single(lines, line => line == "OUT bestmove e2e4");
        Assert.DoesNotContain(
            lines.Skip(Array.IndexOf(lines, "IN quit") + 1),
            line => line.StartsWith("IN position ", StringComparison.Ordinal) ||
                    line.StartsWith("IN go ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParserFailureLeavesAnalyzerReusable()
    {
        await using var host = new TestEngineHost("stockfish malformed first");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<StockfishException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                TestContext.Current.CancellationToken));
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            TestContext.Current.CancellationToken);
        Assert.NotNull(result.BestMove);
    }

    [Fact]
    public async Task InvalidPvLeavesAnalyzerReusable()
    {
        await using var host = new TestEngineHost("stockfish invalid pv first");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<StockfishException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                TestContext.Current.CancellationToken));
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            TestContext.Current.CancellationToken);
        Assert.NotNull(result.BestMove);
    }

    [Fact]
    public async Task ProcessCrashPropagatesAndPermanentlyFaultsAnalyzer()
    {
        await using var host = new TestEngineHost("stockfish exit on search");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<UciEngineException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                TestContext.Current.CancellationToken));
        Assert.Equal(23, exception.ExitCode);
        Assert.Contains(
            "fatal-stockfish-search-message",
            exception.StandardErrorLines);
        await Assert.ThrowsAsync<UciEngineException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                TestContext.Current.CancellationToken));
        Assert.Single(host.ReadLogLines(), line => line == "IN uci");
    }

    [Fact]
    public async Task MultiPvReadinessTimeoutPermanentlyFaultsAnalyzer()
    {
        await using var host =
            new TestEngineHost("stockfish no analysis ready");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(responseTimeout: TimeSpan.FromMilliseconds(200)),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<TimeoutException>(() => analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<UciEngineException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                TestContext.Current.CancellationToken));
        Assert.DoesNotContain(
            host.ReadLogLines(),
            line => line.StartsWith("IN position ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedCancellationCleanupPermanentlyFaultsAnalyzer()
    {
        await using var host = new TestEngineHost("stockfish ignore stop");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(responseTimeout: TimeSpan.FromMilliseconds(200)),
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var analysis = analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            cancellation.Token);
        await host.WaitForLogLineAsync("IN go depth 10");
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => analysis);
        await Assert.ThrowsAsync<UciEngineException>(() =>
            analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                TestContext.Current.CancellationToken));
        Assert.Single(host.ReadLogLines(), line => line == "IN stop");
    }

    [Fact]
    public async Task CancellationDuringMultiPvReadinessIsRecoverable()
    {
        await using var host = new TestEngineHost(
            "stockfish delayed analysis ready");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var first = analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(2),
            cancellation.Token);
        await host.WaitForLogLineAsync("EVENT analysis-ready-held");
        await cancellation.CancelAsync();
        await File.WriteAllTextAsync(
            host.ReleasePath,
            string.Empty,
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        var second = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(3),
            TestContext.Current.CancellationToken);
        Assert.Equal(3, second.Variations.Count);
    }

    [Fact]
    public async Task ConcurrentStressKeepsTransactionsIsolated()
    {
        await using var host = new TestEngineHost();
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var counts = Enumerable
            .Range(0, 20)
            .Select(index => index % 5 + 1)
            .ToArray();
        var tasks = new List<Task<AnalysisResult>>();
        foreach (var count in counts)
        {
            tasks.Add(
                analyzer.AnalyzeAsync(
                    PositionFacts.FromFen(StartFen),
                    Request(count),
                    TestContext.Current.CancellationToken));
        }

        var results = await Task.WhenAll(tasks);
        Assert.Equal(counts, results.Select(result => result.Variations.Count));
        var lines = host.ReadLogLines();
        Assert.Single(lines, line => line == "IN uci");
        Assert.Equal(20, lines.Count(line => line == "IN go depth 10"));
    }

    [Fact]
    public async Task RepeatedSequentialAnalysesRemainIsolated()
    {
        await using var host = new TestEngineHost();
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        for (var index = 0; index < 25; index++)
        {
            var count = index % 5 + 1;
            var result = await analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(count),
                TestContext.Current.CancellationToken);
            Assert.Equal(count, result.Variations.Count);
        }

        Assert.Single(host.ReadLogLines(), line => line == "IN uci");
    }

    [Fact]
    public async Task RepeatedCancellationCyclesDoNotContaminateLaterResult()
    {
        await using var host = new TestEngineHost(
            "stockfish repeated wait for stop");
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        for (var index = 0; index < 5; index++)
        {
            using var cancellation = new CancellationTokenSource();
            var analysis = analyzer.AnalyzeAsync(
                PositionFacts.FromFen(StartFen),
                Request(),
                cancellation.Token);
            await host.WaitForLogLineCountAsync("IN go depth 10", index + 1);

            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                analysis);
        }

        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(),
            TestContext.Current.CancellationToken);
        Assert.Equal(
            1,
            Assert.IsType<CentipawnScore>(
                    Assert.Single(result.Variations)
                        .Score)
                .Centipawns);
        Assert.Equal(
            5,
            host
                .ReadLogLines()
                .Count(line => line == "IN stop"));
    }

    [Fact]
    public async Task LargeIterativeInfoKeepsOnlyLatestLinePerRank()
    {
        await using var host = new TestEngineHost();
        var lines = Enumerable
            .Range(1, 200)
            .SelectMany(depth => new[]
            {
                $"info depth {depth} multipv 1 score cp {depth} pv e2e4",
                $"info depth {depth} multipv 2 score cp {-depth} pv d2d4"
            })
            .Append("bestmove e2e4")
            .ToArray();
        host.SetResponse(lines);
        await using var analyzer = await StockfishAnalyzer.StartAsync(
            host.CreateOptions(),
            TestContext.Current.CancellationToken);
        var result = await analyzer.AnalyzeAsync(
            PositionFacts.FromFen(StartFen),
            Request(2),
            TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Variations.Count);
        Assert.Equal([200, 200], result.Variations.Select(x => x.Depth));
        Assert.Equal(
            [200, -200],
            result.Variations.Select(x => Assert.IsType<CentipawnScore>(x.Score)
                .Centipawns));
    }

    private static AnalysisRequest Request(
        int variationCount = 1)
    {
        return new AnalysisRequest(AnalysisLimit.ByDepth(10), variationCount);
    }
}
