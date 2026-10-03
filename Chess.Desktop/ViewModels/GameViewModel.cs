// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Chess.Analysis;
using Chess.Core.Games;
using Chess.Core.Movement;
using Chess.Desktop.GameModes;
using Chess.Desktop.GameModes.Standard;
using Chess.Desktop.Infrastructure.Commands;
using Chess.Desktop.Presentation.Standard;
using Chess.Variants.Standard;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Sides;

namespace Chess.Desktop.ViewModels;

public sealed class GameViewModel : ViewModelBase, IAsyncDisposable
{
    private static readonly AnalysisRequest EvaluationRequest =
        new(AnalysisLimit.ByTime(TimeSpan.FromMilliseconds(250)));

    private static readonly AnalysisRequest OpponentRequest =
        new(AnalysisLimit.ByTime(TimeSpan.FromSeconds(1)));

    private readonly GameModeDefinition _mode;
    private readonly Game _game;
    private readonly StandardSessionConfiguration _configuration;
    private readonly IStandardEngineSession _session;
    private readonly StandardPositionFactsEvaluator _factsEvaluator;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<Task> _workflows = [];
    private readonly ReadOnlyCollection<SquareViewModel> _squares;
    private CancellationTokenSource? _evaluationCancellation;
    private CancellationTokenSource? _opponentCancellation;
    private SquareViewModel? _selectedSquare;
    private IReadOnlyList<Move> _selectedMoves = [];
    private long _positionGeneration;
    private bool _isDisposing;
    private bool _isBoardFlipped;
    private bool _isEvaluationPending;
    private bool _isEvaluationAvailable;
    private bool _isOpponentThinking;
    private bool _opponentFailed;
    private double _whiteFraction = 0.5;
    private string _evaluationText;
    private string? _evaluationError;
    private string? _opponentError;
    private Task? _disposeTask;

    public IReadOnlyList<SquareViewModel> Squares => _squares;
    public double BoardWidth => _mode.Presentation.Board.Width;
    public double BoardHeight => _mode.Presentation.Board.Height;
    public string ModeName => _game.Variant.Name;

    public string CurrentTurn =>
        _mode.Presentation.GetSideName(_game.State.CurrentSide);

    public string Status => GetStatusText();

    public string PlayDescription =>
        _configuration.IsStockfishGame ? "Stockfish" : "Local Players";

    public string HumanDescription =>
        _configuration.IsStockfishGame
            ? _mode.Presentation.GetSideName(_configuration.HumanSide)
            : "";

    public string DifficultyDescription =>
        _configuration.IsStockfishGame
            ? _configuration.Difficulty.ToString()
            : "";

    public bool IsStockfishGame => _configuration.IsStockfishGame;

    public bool IsBoardInputEnabled =>
        !_isDisposing &&
        !_game.Status.IsTerminal &&
        !_opponentFailed &&
        (!_configuration.IsStockfishGame ||
         (!_isOpponentThinking &&
          _game.State.CurrentSide == _configuration.HumanSide));

    public bool IsBoardFlipped
    {
        get => _isBoardFlipped;
        private set => SetProperty(ref _isBoardFlipped, value);
    }

    public bool IsEvaluationPending
    {
        get => _isEvaluationPending;
        private set => SetProperty(ref _isEvaluationPending, value);
    }

    public bool IsEvaluationAvailable
    {
        get => _isEvaluationAvailable;
        private set => SetProperty(ref _isEvaluationAvailable, value);
    }

    public double WhiteFraction
    {
        get => _whiteFraction;
        private set => SetProperty(ref _whiteFraction, value);
    }

    public string EvaluationText
    {
        get => _evaluationText;
        private set => SetProperty(ref _evaluationText, value);
    }

    public string? EvaluationError
    {
        get => _evaluationError;
        private set => SetProperty(ref _evaluationError, value);
    }

    public string? OpponentError
    {
        get => _opponentError;
        private set => SetProperty(ref _opponentError, value);
    }

    public string EngineStatus
    {
        get
        {
            if (_opponentFailed ||
                (_evaluationError is not null &&
                 !_configuration.IsStockfishGame) ||
                (_session.EvaluationAnalyzer is null &&
                 _session.OpponentAnalyzer is null))
            {
                return "Unavailable";
            }

            return _isOpponentThinking ? "Thinking…" : "Ready";
        }
    }

    public StandardPositionFacts PositionFacts =>
        _factsEvaluator.Evaluate(_game.State);

    public int MoveCount => _game.State.History.Count;
    public ICommand SelectSquareCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand FlipBoardCommand { get; }

    public GameViewModel(
        GameModeDefinition mode,
        StandardSessionConfiguration configuration,
        IStandardEngineSession session,
        Func<Task> goBack)
    {
        _mode = mode ?? throw new ArgumentNullException(nameof(mode));
        _configuration = configuration ??
                         throw new ArgumentNullException(nameof(configuration));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(goBack);
        _game = mode.CreateGame();
        _factsEvaluator = Variant.CreatePositionFactsEvaluator(
            Variant.DefaultInitialState);
        _evaluationText = session.EvaluationAnalyzer is null ? "—" : "…";
        _isEvaluationAvailable = session.EvaluationAnalyzer is not null;
        _squares = Array.AsReadOnly(CreateSquares());
        SelectSquareCommand = new RelayCommand<SquareViewModel>(
            SelectSquare,
            _ => IsBoardInputEnabled);
        BackCommand = new AsyncRelayCommand(
            goBack,
            exception =>
            {
                Trace.TraceError("Game navigation failed: {0}", exception);
                OpponentError =
                    "Unable to leave the game. Close the window to finish cleanup.";
            });
        FlipBoardCommand = new RelayCommand(FlipBoard);
        if (configuration.IsStockfishGame &&
            configuration.HumanSide == SideDefinitions.Black)
        {
            FlipBoard();
        }

        RefreshBoard();
        StartPositionWork();
    }

    private SquareViewModel[] CreateSquares()
    {
        var board = _mode.Presentation.Board;
        return
        [
            .. board.Squares.Select(square => new SquareViewModel(
                square.Square,
                square.X,
                square.Y,
                board.SquareSize,
                square.IsLightSquare))
        ];
    }

    public void FlipBoard()
    {
        if (_isDisposing)
        {
            return;
        }

        IsBoardFlipped = !IsBoardFlipped;
        foreach (var square in _squares)
        {
            square.SetOrientation(IsBoardFlipped, BoardWidth, BoardHeight);
        }
    }

    private void SelectSquare(
        SquareViewModel square)
    {
        if (!IsBoardInputEnabled)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(square);
        if (ReferenceEquals(square, _selectedSquare))
        {
            ClearSelection();
            return;
        }

        var move = _selectedSquare is null
            ? null
            : _selectedMoves
                .Where(candidate => candidate.To == square.Square)
                .Select(candidate => (Move?)candidate)
                .FirstOrDefault();
        if (move is { } selectedMove)
        {
            ExecuteMove(selectedMove);
            return;
        }

        if (!_game.BoardState.TryGetPiece(square.Square, out var piece) ||
            piece.Side != _game.State.CurrentSide)
        {
            ClearSelection();
            return;
        }

        _selectedSquare = square;
        _selectedMoves = [.. _game.GenerateMoves(square.Square)];
        RefreshSelection();
    }

    private void ExecuteMove(
        Move move)
    {
        _game.Execute(move);
        ClearSelection();
        RefreshBoard();
        OnPropertyChanged(nameof(CurrentTurn));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(PositionFacts));
        OnPropertyChanged(nameof(IsBoardInputEnabled));
        StartPositionWork();
    }

    private void StartPositionWork()
    {
        _positionGeneration++;
        _evaluationCancellation?.Cancel();
        if (_game.Status.IsTerminal)
        {
            IsEvaluationPending = false;
            var winners = _game.Outcome!.Winners;
            WhiteFraction = winners.Count == 0 ? 0.5 :
                winners.Contains(SideDefinitions.White) ? 1 : 0;
            EvaluationText = winners.Count == 0 ? "½–½" :
                winners.Contains(SideDefinitions.White) ? "1–0" : "0–1";
            OnPropertyChanged(nameof(IsBoardInputEnabled));
            return;
        }

        var generation = _positionGeneration;
        var facts = _factsEvaluator.Evaluate(_game.State);
        if (_session.EvaluationAnalyzer is { } evaluation &&
            _evaluationError is null)
        {
            IsEvaluationPending = true;
            var cancellation =
                CancellationTokenSource
                    .CreateLinkedTokenSource(_lifetime.Token);
            _evaluationCancellation = cancellation;
            Track(EvaluateAsync(evaluation, facts, generation, cancellation));
        }

        if (_configuration.IsStockfishGame &&
            _game.State.CurrentSide != _configuration.HumanSide &&
            !_opponentFailed)
        {
            if (_session.OpponentAnalyzer is { } opponent)
            {
                _isOpponentThinking = true;
                OnPropertyChanged(nameof(EngineStatus));
                OnPropertyChanged(nameof(IsBoardInputEnabled));
                var cancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        _lifetime.Token);
                _opponentCancellation = cancellation;
                Track(
                    PlayOpponentAsync(
                        opponent,
                        facts,
                        generation,
                        cancellation));
            }
            else
            {
                SetOpponentError("Stockfish opponent is unavailable.");
            }
        }
    }

    private async Task EvaluateAsync(
        IPositionAnalyzer<StandardPositionFacts> analyzer,
        StandardPositionFacts facts,
        long generation,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await analyzer.AnalyzeAsync(
                facts,
                EvaluationRequest,
                cancellation.Token);
            if (!IsCurrent(generation))
            {
                return;
            }

            if (result.Variations.Count == 0)
            {
                throw new InvalidOperationException(
                    "Stockfish returned no evaluation score.");
            }

            var presentation = EvaluationPresentation.FromResult(result);
            WhiteFraction = presentation.WhiteFraction;
            EvaluationText = presentation.Text;
            IsEvaluationPending = false;
        }
        catch (OperationCanceledException) when (cancellation
                                                     .IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrent(generation))
            {
                IsEvaluationAvailable = false;
                IsEvaluationPending = false;
                EvaluationText = "—";
                Trace.TraceError("Evaluation failed: {0}", exception);
                EvaluationError = "Evaluation unavailable.";
                OnPropertyChanged(nameof(EngineStatus));
            }
        }
        finally
        {
            if (ReferenceEquals(_evaluationCancellation, cancellation))
            {
                _evaluationCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async Task PlayOpponentAsync(
        IPositionAnalyzer<StandardPositionFacts> analyzer,
        StandardPositionFacts facts,
        long generation,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await analyzer.AnalyzeAsync(
                facts,
                OpponentRequest,
                cancellation.Token);
            if (!IsCurrent(generation) ||
                _game.Status.IsTerminal ||
                _game.State.CurrentSide == _configuration.HumanSide)
            {
                return;
            }

            if (result.BestMove is not { } move)
            {
                SetOpponentError(
                    "Stockfish returned no move for an active game.");
                return;
            }

            _opponentCancellation = null;
            ExecuteMove(move);
        }
        catch (OperationCanceledException) when (cancellation
                                                     .IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrent(generation))
            {
                Trace.TraceError("Stockfish move failed: {0}", exception);
                SetOpponentError(
                    "Stockfish move failed. Return to setup to try again.");
            }
        }
        finally
        {
            if (ReferenceEquals(_opponentCancellation, cancellation))
            {
                _opponentCancellation = null;
            }

            cancellation.Dispose();
            if (!_isDisposing)
            {
                _isOpponentThinking = false;
                OnPropertyChanged(nameof(EngineStatus));
                OnPropertyChanged(nameof(IsBoardInputEnabled));
            }
        }
    }

    private bool IsCurrent(
        long generation) =>
        !_isDisposing && generation == _positionGeneration;

    private void Track(
        Task workflow)
    {
        _workflows.Add(workflow);
        _workflows.RemoveAll(task => task.IsCompleted);
    }

    internal Task WaitForWorkflowsAsync() => Task.WhenAll(_workflows.ToArray());

    private void SetOpponentError(
        string message)
    {
        _opponentFailed = true;
        OpponentError = message;
        OnPropertyChanged(nameof(EngineStatus));
        OnPropertyChanged(nameof(IsBoardInputEnabled));
    }

    private void RefreshBoard()
    {
        foreach (var square in _squares)
        {
            square.PieceImageSource =
                _game.BoardState.TryGetPiece(square.Square, out var piece)
                    ? _mode.Presentation.GetPieceImageSource(piece)
                    : null;
        }
    }

    private void ClearSelection()
    {
        _selectedSquare = null;
        _selectedMoves = [];
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        var destinations = _selectedMoves
            .Select(move => move.To)
            .ToHashSet();
        foreach (var square in _squares)
        {
            square.IsSelected = ReferenceEquals(square, _selectedSquare);
            var legal = destinations.Contains(square.Square);
            var occupied = legal && _game.BoardState.IsOccupied(square.Square);
            square.IsLegalDestination = legal && !occupied;
            square.IsCaptureDestination = legal && occupied;
        }
    }

    private string GetStatusText()
    {
        var status = _game.Status;
        return status.Outcome is { } outcome
            ? _mode.Presentation.GetTerminationName(outcome.Termination)
            : _mode.Presentation.GetStatusName(status.Id);
    }

    public ValueTask DisposeAsync()
    {
        _disposeTask ??= DisposeCoreAsync();
        return new ValueTask(_disposeTask);
    }

    private async Task DisposeCoreAsync()
    {
        _isDisposing = true;
        _positionGeneration++;
        OnPropertyChanged(nameof(IsBoardInputEnabled));
        _evaluationCancellation?.Cancel();
        _opponentCancellation?.Cancel();
        await _lifetime.CancelAsync();
        try
        {
            await Task.WhenAll(_workflows.ToArray());
        }
        finally
        {
            await _session.DisposeAsync();
            _lifetime.Dispose();
        }
    }
}
