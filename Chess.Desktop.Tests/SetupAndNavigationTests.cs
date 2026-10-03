// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Analysis;
using Chess.Desktop.GameModes;
using Chess.Desktop.GameModes.Standard;
using Chess.Desktop.Infrastructure.Commands;
using Chess.Desktop.ViewModels;
using Chess.Stockfish;
using Chess.Variants.Standard.Games.History;

namespace Chess.Desktop.Tests;

public sealed class SetupAndNavigationTests
{
    [Fact]
    public async Task EmptyPathIsAllowedLocallyButRejectedForOpponent()
    {
        var factory = new RecordingFactory();
        var started = 0;
        var setup = new StandardGameSetupViewModel(
            factory,
            new FakePicker(),
            (_, _) =>
            {
                started++;
                return Task.CompletedTask;
            },
            () =>
            {
            },
            "");
        await setup.StartAsync();
        Assert.Equal(1, started);
        Assert.Single(factory.Configurations);
        setup.SessionType = StandardSessionType.PlayerVsStockfish;
        await setup.StartAsync();
        Assert.Equal(1, started);
        Assert.Single(factory.Configurations);
        Assert.Contains("Choose a Stockfish executable", setup.ErrorMessage);
        await setup.DisposeAsync();
    }

    [Fact]
    public async Task InvalidStartupLeavesSetupWithError()
    {
        var factory = new RecordingFactory
        {
            Failure = new StockfishException("bad engine")
        };
        var setup = new StandardGameSetupViewModel(
            factory,
            new FakePicker(),
            (_, _) => throw new Exception("Game must not open."),
            () =>
            {
            },
            "invalid.exe");
        setup.SessionType = StandardSessionType.PlayerVsStockfish;
        await setup.StartAsync();
        Assert.Contains("could not start", setup.ErrorMessage);
        await setup.DisposeAsync();
    }

    [Fact]
    public async Task BrowseIsInjectableAndStartPreventsReentry()
    {
        var factory = new RecordingFactory();
        var pending = new TaskCompletionSource<IStandardEngineSession>();
        factory.Pending = pending.Task;
        var started = 0;
        var setup = new StandardGameSetupViewModel(
            factory,
            new FakePicker("chosen.exe"),
            (_, _) =>
            {
                started++;
                return Task.CompletedTask;
            },
            () =>
            {
            },
            "");
        setup.BrowseCommand.Execute(null);
        Assert.Equal("chosen.exe", setup.StockfishPath);
        var first = setup.StartAsync();
        Assert.True(setup.IsStarting);
        Assert.False(setup.StartCommand.CanExecute(null));
        Assert.False(setup.BackCommand.CanExecute(null));
        await setup.StartAsync();
        Assert.Single(factory.Configurations);
        pending.SetResult(new RecordingSession());
        await first;
        Assert.Equal(1, started);
        Assert.True(setup.StartCommand.CanExecute(null));
        await setup.DisposeAsync();
    }

    [Fact]
    public async Task NavigationReturnsToPreservedSetupAfterDisposal()
    {
        var factory = new RecordingFactory();
        var shell = new ShellViewModel(
            new GameModeCatalog([new StandardGameModeProvider()]),
            factory,
            new FakePicker());
        var modes = Assert.IsType<ModeSelectionViewModel>(shell.CurrentScreen);
        modes
            .Modes
            .Single()
            .SelectCommand
            .Execute(null);
        var setup = Assert.IsType<StandardGameSetupViewModel>(
            shell.CurrentScreen);
        await setup.StartAsync();
        var game = Assert.IsType<GameViewModel>(shell.CurrentScreen);
        await ((AsyncRelayCommand)game.BackCommand).ExecuteAsync();
        Assert.Same(setup, shell.CurrentScreen);
        Assert.True(
            Assert.Single(factory.Sessions)
                .Disposed);
        setup.BackCommand.Execute(null);
        Assert.IsType<ModeSelectionViewModel>(shell.CurrentScreen);
        await shell.DisposeAsync();
    }

    [Fact]
    public async Task WindowLifetimeOwnerDisposesCurrentGame()
    {
        var factory = new RecordingFactory();
        var shell = new ShellViewModel(
            new GameModeCatalog([new StandardGameModeProvider()]),
            factory,
            new FakePicker());
        ((ModeSelectionViewModel)shell.CurrentScreen)
            .Modes
            .Single()
            .SelectCommand
            .Execute(null);
        await ((StandardGameSetupViewModel)shell.CurrentScreen).StartAsync();
        await shell.DisposeAsync();
        Assert.True(
            Assert.Single(factory.Sessions)
                .Disposed);
    }

    [Fact]
    public async Task ShellDisposesCachedSetupFromModeSelection()
    {
        var factory = new RecordingFactory();
        var shell = CreateShell(factory);
        var setup = EnterSetup(shell);

        setup.BackCommand.Execute(null);
        Assert.IsType<ModeSelectionViewModel>(shell.CurrentScreen);

        await shell.DisposeAsync();
        await setup.StartAsync();

        Assert.Empty(factory.Configurations);
    }

    [Fact]
    public async Task ShellDisposesCachedSetupWhenGameIsCurrent()
    {
        var factory = new RecordingFactory();
        var shell = CreateShell(factory);
        var setup = EnterSetup(shell);
        await setup.StartAsync();

        Assert.IsType<GameViewModel>(shell.CurrentScreen);
        var configurationCount = factory.Configurations.Count;
        await shell.DisposeAsync();
        await setup.StartAsync();

        Assert.Equal(configurationCount, factory.Configurations.Count);
        Assert.True(
            Assert.Single(factory.Sessions)
                .Disposed);
    }

    [Fact]
    public async Task CloseWaitsForBackCleanupAlreadyInProgress()
    {
        var factory = new RecordingFactory();
        var shell = new ShellViewModel(
            new GameModeCatalog([new StandardGameModeProvider()]),
            factory,
            new FakePicker());
        ((ModeSelectionViewModel)shell.CurrentScreen)
            .Modes
            .Single()
            .SelectCommand
            .Execute(null);
        await ((StandardGameSetupViewModel)shell.CurrentScreen).StartAsync();
        var game = (GameViewModel)shell.CurrentScreen;
        var session = Assert.Single(factory.Sessions);
        var release = new TaskCompletionSource();
        session.DisposalGate = release.Task;
        var back = ((AsyncRelayCommand)game.BackCommand).ExecuteAsync();
        var close = shell
            .DisposeAsync()
            .AsTask();
        Assert.False(back.IsCompleted);
        Assert.False(close.IsCompleted);
        release.SetResult();
        await Task.WhenAll(back, close);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task AsyncCommandLocksAndObservesErrors()
    {
        var gate = new TaskCompletionSource();
        var count = 0;
        Exception? observed = null;
        var command = new AsyncRelayCommand(
            async () =>
            {
                count++;
                await gate.Task;
            },
            exception => observed = exception);
        var first = command.ExecuteAsync();
        Assert.False(command.CanExecute(null));
        await command.ExecuteAsync();
        Assert.Equal(1, count);
        gate.SetResult();
        await first;
        Assert.True(command.CanExecute(null));

        var errorGate = new TaskCompletionSource();
        var failing = new AsyncRelayCommand(
            () => Task.FromException(new InvalidOperationException("bad")),
            exception =>
            {
                observed = exception;
                errorGate.SetResult();
            });
        failing.Execute(null);
        await errorGate.Task;
        Assert.IsType<InvalidOperationException>(observed);
        Assert.True(failing.CanExecute(null));

        var canceled = new TaskCompletionSource();
        var cancelable = new AsyncRelayCommand(
            () => canceled.Task,
            _ =>
            {
            });
        var cancellation = cancelable.ExecuteAsync();
        Assert.False(cancelable.CanExecute(null));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        source.Cancel();
        canceled.SetCanceled(source.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cancellation);
        Assert.True(cancelable.CanExecute(null));
    }

    [Fact]
    public async Task ClosingSetupCancelsStartupAndDisposesLateSession()
    {
        var factory = new RecordingFactory();
        var pending = new TaskCompletionSource<IStandardEngineSession>();
        factory.Pending = pending.Task;
        var opened = false;
        var setup = new StandardGameSetupViewModel(
            factory,
            new FakePicker(),
            (_, _) =>
            {
                opened = true;
                return Task.CompletedTask;
            },
            () =>
            {
            },
            "stockfish.exe");
        var start = setup.StartAsync();
        var close = setup
            .DisposeAsync()
            .AsTask();
        Assert.False(close.IsCompleted);
        var late = new RecordingSession();
        pending.SetResult(late);
        await Task.WhenAll(start, close);
        Assert.True(late.Disposed);
        Assert.False(opened);
    }

    private static ShellViewModel CreateShell(
        RecordingFactory factory) =>
        new(
            new GameModeCatalog([new StandardGameModeProvider()]),
            factory,
            new FakePicker());

    private static StandardGameSetupViewModel EnterSetup(
        ShellViewModel shell)
    {
        var modes = Assert.IsType<ModeSelectionViewModel>(shell.CurrentScreen);
        modes
            .Modes
            .Single()
            .SelectCommand
            .Execute(null);
        return Assert.IsType<StandardGameSetupViewModel>(shell.CurrentScreen);
    }

    private sealed class FakePicker(string? selected = null)
        : IStockfishExecutablePicker
    {
        public string? Pick(
            string currentPath) =>
            selected;
    }

    private sealed class RecordingFactory : IStandardEngineSessionFactory
    {
        public List<StandardSessionConfiguration> Configurations { get; } = [];
        public List<RecordingSession> Sessions { get; } = [];
        public Task<IStandardEngineSession>? Pending { get; set; }
        public Exception? Failure { get; set; }

        public async Task<IStandardEngineSession> StartAsync(
            StandardSessionConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            Configurations.Add(configuration);
            if (Failure is { } exception)
            {
                throw exception;
            }

            if (Pending is { } pending)
            {
                return await pending;
            }

            var session = new RecordingSession();
            Sessions.Add(session);
            return session;
        }
    }

    private sealed class RecordingSession : IStandardEngineSession
    {
        public IPositionAnalyzer<StandardPositionFacts>? EvaluationAnalyzer =>
            null;

        public IPositionAnalyzer<StandardPositionFacts>? OpponentAnalyzer =>
            null;

        public bool Disposed { get; private set; }
        public Task? DisposalGate { get; set; }

        public async ValueTask DisposeAsync()
        {
            if (DisposalGate is { } gate)
            {
                await gate;
            }

            Disposed = true;
        }
    }
}
