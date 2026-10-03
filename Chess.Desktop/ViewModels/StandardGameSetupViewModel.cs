// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Chess.Core.Sides;
using Chess.Desktop.GameModes.Standard;
using Chess.Desktop.Infrastructure.Commands;
using Chess.Stockfish;
using Chess.Uci;
using Chess.Variants.Standard.Sides;

namespace Chess.Desktop.ViewModels;

public sealed class StandardGameSetupViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IStandardEngineSessionFactory _sessionFactory;
    private readonly IStockfishExecutablePicker _picker;

    private readonly Func<StandardSessionConfiguration, IStandardEngineSession,
        Task> _onStarted;

    private StandardSessionType _sessionType =
        StandardSessionType.PlayerVsPlayer;

    private Side _humanSide = SideDefinitions.White;
    private StandardDifficulty _difficulty = StandardDifficulty.Medium;
    private string _stockfishPath;
    private string? _errorMessage;
    private bool _isStarting;
    private bool _disposed;
    private readonly CancellationTokenSource _startupCancellation = new();
    private TaskCompletionSource? _startupCompletion;

    public IReadOnlyList<StandardSessionType> SessionTypes { get; } =
        Enum.GetValues<StandardSessionType>();

    public IReadOnlyList<StandardDifficulty> Difficulties { get; } =
        Enum.GetValues<StandardDifficulty>();

    public IReadOnlyList<Side> HumanSides { get; } =
    [
        SideDefinitions.White, SideDefinitions.Black
    ];

    public StandardSessionType SessionType
    {
        get => _sessionType;
        set
        {
            if (SetProperty(ref _sessionType, value))
            {
                OnPropertyChanged(nameof(IsStockfishGame));
                OnPropertyChanged(nameof(IsLocalSelected));
                OnPropertyChanged(nameof(IsStockfishSelected));
                OnPropertyChanged(nameof(PathHint));
            }
        }
    }

    public bool IsStockfishGame =>
        SessionType == StandardSessionType.PlayerVsStockfish;

    public bool IsLocalSelected
    {
        get => !IsStockfishGame;
        set
        {
            if (value)
            {
                SessionType = StandardSessionType.PlayerVsPlayer;
            }
        }
    }

    public bool IsStockfishSelected
    {
        get => IsStockfishGame;
        set
        {
            if (value)
            {
                SessionType = StandardSessionType.PlayerVsStockfish;
            }
        }
    }

    public bool IsHumanWhite
    {
        get => HumanSide == SideDefinitions.White;
        set
        {
            if (value)
            {
                HumanSide = SideDefinitions.White;
            }
        }
    }

    public bool IsHumanBlack
    {
        get => HumanSide == SideDefinitions.Black;
        set
        {
            if (value)
            {
                HumanSide = SideDefinitions.Black;
            }
        }
    }

    public Side HumanSide
    {
        get => _humanSide;
        set
        {
            if (SetProperty(ref _humanSide, value))
            {
                OnPropertyChanged(nameof(IsHumanWhite));
                OnPropertyChanged(nameof(IsHumanBlack));
            }
        }
    }

    public StandardDifficulty Difficulty
    {
        get => _difficulty;
        set => SetProperty(ref _difficulty, value);
    }

    public string StockfishPath
    {
        get => _stockfishPath;
        set => SetProperty(ref _stockfishPath, value);
    }

    public string PathHint =>
        IsStockfishGame
            ? "Required to play against Stockfish"
            : "Optional: enables full-strength evaluation";

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsStarting
    {
        get => _isStarting;
        private set
        {
            if (SetProperty(ref _isStarting, value))
            {
                _startCommand.RefreshCanExecute();
                _backCommand.RefreshCanExecute();
            }
        }
    }

    private readonly AsyncRelayCommand _startCommand;
    private readonly RelayCommand _backCommand;

    public ICommand StartCommand => _startCommand;

    public ICommand BackCommand => _backCommand;

    public ICommand BrowseCommand { get; }

    public StandardGameSetupViewModel(
        IStandardEngineSessionFactory sessionFactory,
        IStockfishExecutablePicker picker,
        Func<StandardSessionConfiguration, IStandardEngineSession, Task>
            onStarted,
        Action goBack,
        string? initialPath = null)
    {
        _sessionFactory = sessionFactory;
        _picker = picker;
        _onStarted = onStarted;
        _stockfishPath = initialPath ?? StockfishPathDiscovery.Discover();
        _startCommand = new AsyncRelayCommand(
            StartAsync,
            exception =>
            {
                Trace.TraceError("Game startup failed: {0}", exception);
                ErrorMessage =
                    "Unable to start the game. Check the selected settings.";
            },
            () => !IsStarting);
        _backCommand = new RelayCommand(goBack, () => !IsStarting);
        BrowseCommand = new RelayCommand(Browse, () => !IsStarting);
    }

    private void Browse()
    {
        var selected = _picker.Pick(StockfishPath);
        if (selected is not null)
        {
            StockfishPath = selected;
        }
    }

    public async Task StartAsync()
    {
        if (IsStarting || _disposed)
        {
            return;
        }

        ErrorMessage = null;
        if (IsStockfishGame && string.IsNullOrWhiteSpace(StockfishPath))
        {
            ErrorMessage = "Choose a Stockfish executable to play against it.";
            return;
        }

        IsStarting = true;
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _startupCompletion = completion;
        try
        {
            var configuration = new StandardSessionConfiguration(
                SessionType,
                HumanSide,
                Difficulty,
                StockfishPath.Trim());
            var session = await _sessionFactory.StartAsync(
                configuration,
                _startupCancellation.Token);
            try
            {
                _startupCancellation.Token.ThrowIfCancellationRequested();
                await _onStarted(configuration, session);
            }
            catch
            {
                await session.DisposeAsync();
                throw;
            }
        }
        catch (OperationCanceledException) when (_startupCancellation
                                                     .IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException
                                              or UciEngineException
                                              or StockfishException
                                              or TimeoutException
                                              or Win32Exception
                                              or ArgumentException
                                              or ObjectDisposedException)
        {
            Trace.TraceError("Stockfish startup failed: {0}", exception);
            ErrorMessage = exception is ArgumentOutOfRangeException
                ? "Stockfish could not start: this build does not support the selected difficulty."
                : "Stockfish could not start. Check the executable path and engine compatibility.";
        }
        finally
        {
            IsStarting = false;
            completion.TrySetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _startupCancellation.CancelAsync();
        if (_startupCompletion is { } completion)
        {
            await completion.Task;
        }

        _startupCancellation.Dispose();
    }
}
