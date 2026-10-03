// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Uci.Tests;

public sealed class UciEngineLifecycleTests
{
    [Fact]
    public async Task StartAsync_CompletesHandshakeAndCapturesEngineInfo()
    {
        await using var host = new TestEngineHost("normal scenario");

        await using var engine = await host.StartAsync();

        Assert.Equal("Chess Fake UCI Engine", engine.Info.Name);
        Assert.Equal("Chess.Tests", engine.Info.Author);
        Assert.Equal(
            [
                "option name Hash type spin default 16 min 1 max 1024",
                "option name Clear Hash type button"
            ],
            engine.Info.OptionLines);
        Assert.Contains("OUT fake engine banner", host.ReadLogLines());
        Assert.Contains("OUT info string initializing", host.ReadLogLines());
    }

    [Fact]
    public async Task
        StartAsync_PreservesArgumentsContainingSpacesAndWorkingDirectory()
    {
        await using var host = new TestEngineHost("normal scenario");

        await using var engine = await host.StartAsync();

        Assert.Equal("Chess Fake UCI Engine", engine.Info.Name);
    }

    [Fact]
    public async Task StartAsync_ReportsExitCodeAndStandardError()
    {
        await using var host = new TestEngineHost("exit before uciok");

        var exception = await Assert.ThrowsAsync<UciEngineException>(() =>
            UciEngineProcess.StartAsync(
                host.CreateOptions(),
                TestContext.Current.CancellationToken));

        Assert.Equal(17, exception.ExitCode);
        Assert.Contains("fatal-test-message", exception.StandardErrorLines);
    }

    [Fact]
    public async Task StartAsync_DrainsStandardErrorFlood()
    {
        await using var host = new TestEngineHost("stderr flood");

        await using var engine = await host.StartAsync();

        Assert.Equal("Chess Fake UCI Engine", engine.Info.Name);
    }

    [Fact]
    public async Task StartAsync_TimesOutAndCleansUpMissingUciOk()
    {
        await using var host = new TestEngineHost("no uciok");

        await Assert.ThrowsAsync<TimeoutException>(() =>
            UciEngineProcess.StartAsync(
                host.CreateOptions(responseTimeout: TimeSpan.FromSeconds(2)),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitUntilReadyAsync_IgnoresDiagnostics()
    {
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();

        await engine.WaitUntilReadyAsync(TestContext.Current.CancellationToken);

        var lines = host.ReadLogLines();
        Assert.Contains("IN isready", lines);
        Assert.Contains("OUT info string ready diagnostic", lines);
        Assert.Contains("OUT readyok", lines);
    }

    [Fact]
    public async Task
        WaitUntilReadyAsync_ResynchronizesAfterCallerCancellation()
    {
        await using var host = new TestEngineHost("delayed ready");
        await using var engine = await host.StartAsync();
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
        var releasePath = host.ReleasePath;
        await using var registration = cancellation.Token.Register(
            static state => File
                .Create((string)state!)
                .Dispose(),
            releasePath);

        var readyTask = engine.WaitUntilReadyAsync(cancellation.Token);
        await host.WaitForLogLineAsync("IN isready");
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            readyTask);
        await engine.WaitUntilReadyAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WaitUntilReadyAsync_TimeoutFaultsEngine()
    {
        await using var host = new TestEngineHost("no readyok");
        await using var engine = await host.StartAsync(
            responseTimeout: TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            engine.WaitUntilReadyAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<UciEngineException>(() =>
            engine.WaitUntilReadyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitUntilReadyAsync_ReportsUnexpectedProcessExit()
    {
        await using var host = new TestEngineHost("exit on ready");
        await using var engine = await host.StartAsync();

        var exception = await Assert.ThrowsAsync<UciEngineException>(() =>
            engine.WaitUntilReadyAsync(TestContext.Current.CancellationToken));

        Assert.Equal(17, exception.ExitCode);
        Assert.Contains("fatal-ready-message", exception.StandardErrorLines);
    }

    [Fact]
    public async Task WaitUntilReadyAsync_FailedCancellationResyncFaultsEngine()
    {
        await using var host = new TestEngineHost("no readyok");
        await using var engine = await host.StartAsync(
            responseTimeout: TimeSpan.FromSeconds(2));
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
        var ready = engine.WaitUntilReadyAsync(cancellation.Token);
        await host.WaitForLogLineAsync("IN isready");

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => ready);
        await Assert.ThrowsAsync<UciEngineException>(() =>
            engine.WaitUntilReadyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task QuitAsync_IsGracefulIdempotentAndClosesOperations()
    {
        await using var host = new TestEngineHost("normal scenario");
        var engine = await host.StartAsync();

        await engine.QuitAsync(TestContext.Current.CancellationToken);
        await engine.QuitAsync(TestContext.Current.CancellationToken);
        await engine.DisposeAsync();

        Assert.Equal(
            1,
            host
                .ReadLogLines()
                .Count(line => line == "IN quit"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            engine.WaitUntilReadyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task QuitAsync_PreventsQueuedSearchFromStarting()
    {
        await using var host = new TestEngineHost("delayed ready");
        var engine = await host.StartAsync(
            shutdownTimeout: TimeSpan.FromSeconds(5));
        var ready = engine.WaitUntilReadyAsync(
            TestContext.Current.CancellationToken);
        await host.WaitForLogLineAsync("IN isready");
        var search = engine.SearchAsync(
            UciPosition.FromStartPosition(),
            UciGoCommand.ByDepth(10),
            TestContext.Current.CancellationToken);
        Assert.False(search.IsCompleted);

        var quit = engine.QuitAsync(TestContext.Current.CancellationToken);
        Assert.False(quit.IsCompleted);
        await File.WriteAllTextAsync(
            host.ReleasePath,
            string.Empty,
            TestContext.Current.CancellationToken);

        await ready;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => search);
        await quit;

        var lines = host.ReadLogLines();
        Assert.DoesNotContain(
            lines,
            line => line.StartsWith("IN position ", StringComparison.Ordinal));
        Assert.DoesNotContain(
            lines,
            line => line.StartsWith("IN go ", StringComparison.Ordinal));
        Assert.DoesNotContain("IN stop", lines);
        Assert.Contains("IN quit", lines);
        Assert.Contains("EVENT graceful-exit", lines);
    }

    [Fact]
    public async Task QuitAsync_ForceKillsUnresponsiveProcess()
    {
        await using var host = new TestEngineHost("ignore quit");
        var engine = await host.StartAsync(
            shutdownTimeout: TimeSpan.FromMilliseconds(200));

        await engine.QuitAsync(TestContext.Current.CancellationToken);
        await engine.DisposeAsync();

        Assert.Contains("IN quit", host.ReadLogLines());
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotentAndClosesOperations()
    {
        await using var host = new TestEngineHost("normal scenario");
        var engine = await host.StartAsync();

        await engine.DisposeAsync();
        await engine.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            engine.NewGameAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisposeAsync_InterruptsAndObservesPendingOperation()
    {
        await using var host = new TestEngineHost("no readyok");
        var engine = await host.StartAsync(
            shutdownTimeout: TimeSpan.FromMilliseconds(200));
        var ready = engine.WaitUntilReadyAsync(
            TestContext.Current.CancellationToken);
        await host.WaitForLogLineAsync("IN isready");

        await engine.DisposeAsync();

        await Assert.ThrowsAsync<UciEngineException>(() => ready);
    }

    [Fact]
    public void UciEngineException_DefensivelyCopiesStandardErrorLines()
    {
        var lines = new List<string> { "first" };
        var exception = new UciEngineException("failure", 17, lines);

        lines[0] = "changed";

        Assert.Equal(["first"], exception.StandardErrorLines);
        Assert.Equal(17, exception.ExitCode);
    }

    [Fact]
    public void UciEngineInfo_DefensivelyCopiesOptionLines()
    {
        var lines = new List<string> { "option name Hash type spin" };
        var info = new UciEngineInfo("name", "author", lines);

        lines[0] = "changed";

        Assert.Equal(["option name Hash type spin"], info.OptionLines);
    }
}
