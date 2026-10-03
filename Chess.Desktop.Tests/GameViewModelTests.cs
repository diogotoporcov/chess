// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.IO;
using Chess.Analysis;
using Chess.Core.Sides;
using Chess.Desktop.GameModes;
using Chess.Desktop.GameModes.Standard;
using Chess.Desktop.ViewModels;
using Chess.Variants.Standard;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Notation;
using Chess.Variants.Standard.Notation.Fen;
using Chess.Variants.Standard.Notation.Uci;
using Chess.Variants.Standard.Sides;

namespace Chess.Desktop.Tests;

public sealed class GameViewModelTests
{
    private static readonly GameModeDefinition Mode =
        new StandardGameModeProvider()
            .GetModes()
            .Single();

    [Fact]
    public async Task
        LocalWithoutEngineAllowsBothSidesAndFlipPreservesPosition()
    {
        await using var game = Create(StandardSessionConfiguration.Local());
        Assert.True(game.IsBoardInputEnabled);
        Assert.Equal("—", game.EvaluationText);
        var fen = new FenCodec().Format(game.PositionFacts);
        var original = game
            .Squares
            .Select(square => (square.X, square.Y))
            .ToArray();
        var status = game.Status;
        game.FlipBoard();
        foreach (var square in game.Squares)
        {
            Assert.Equal(
                game.BoardWidth - square.OriginalX - square.Size,
                square.X);
            Assert.Equal(
                game.BoardHeight - square.OriginalY - square.Size,
                square.Y);
        }

        Assert.Equal(fen, new FenCodec().Format(game.PositionFacts));
        Assert.Equal(status, game.Status);
        Assert.Equal(0, game.MoveCount);
        game.FlipBoard();
        Assert.Equal(
            original,
            game
                .Squares
                .Select(square => (square.X, square.Y))
                .ToArray());
        ClickMove(game, "e2", "e4");
        Assert.True(game.IsBoardInputEnabled);
        ClickMove(game, "e7", "e5");
        Assert.Equal(2, game.MoveCount);
        Assert.Equal("White", game.CurrentTurn);
    }

    [Fact]
    public async Task
        EvaluationStartsInitiallyAndStaleResultCannotOverwriteNewer()
    {
        var evaluator = new ControlledAnalyzer(ignoreCancellation: true);
        await using var game = Create(
            StandardSessionConfiguration.Local(),
            evaluator);
        Assert.Single(evaluator.Calls);
        Assert.Equal(1, evaluator.Calls[0].Request.VariationCount);
        Assert.Equal(
            TimeSpan.FromMilliseconds(250),
            evaluator.Calls[0].Request.Limit.Time);
        Assert.True(game.IsEvaluationPending);
        Assert.Equal("…", game.EvaluationText);
        evaluator.Complete(0, Score(10, SideDefinitions.White));
        await WaitForAsync(game, () => game.EvaluationText == "+0.10");
        ClickMove(game, "e2", "e4");
        Assert.Equal(2, evaluator.Calls.Count);
        Assert.True(game.IsEvaluationPending);
        Assert.Equal("+0.10", game.EvaluationText);
        ClickMove(game, "e7", "e5");
        Assert.Equal(3, evaluator.Calls.Count);
        Assert.NotEqual(
            evaluator.Calls[1].Position,
            evaluator.Calls[2].Position);
        Assert.True(evaluator.Calls[1].Canceled);
        evaluator.Complete(2, Score(50, SideDefinitions.White));
        await WaitForAsync(game, () => game.EvaluationText == "+0.50");
        var fraction = game.WhiteFraction;
        evaluator.Complete(1, Score(-200, SideDefinitions.Black));
        await game.WaitForWorkflowsAsync();
        Assert.Equal("+0.50", game.EvaluationText);
        Assert.Equal(fraction, game.WhiteFraction);
    }

    [Fact]
    public async Task HumanWhiteLocksInputAndUsesFinalBestMove()
    {
        var opponent = new ControlledAnalyzer();
        await using var game = Create(
            Ai(SideDefinitions.White),
            null,
            opponent);
        Assert.True(game.IsBoardInputEnabled);
        ClickMove(game, "e2", "e4");
        Assert.Equal(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            new FenCodec().Format(game.PositionFacts));
        Assert.False(game.IsBoardInputEnabled);
        Assert.Single(opponent.Calls);
        Assert.Equal(1, opponent.Calls[0].Request.VariationCount);
        Assert.Equal(
            TimeSpan.FromSeconds(1),
            opponent.Calls[0].Request.Limit.Time);
        var separate = Variant.CreateGame();
        separate.Execute(new UciMoveCodec().Parse(separate, "e2e4"));
        var best = new UciMoveCodec().Parse(separate, "e7e5");
        var pv = new UciMoveCodec().Parse(separate, "e7e6");
        opponent.Complete(
            0,
            new AnalysisResult(
                SideDefinitions.Black,
                best,
                new AnalysisVariation(
                    new CentipawnScore(10),
                    AnalysisScoreBound.Exact,
                    [pv])));
        await WaitForAsync(game, () => game.MoveCount == 2);
        await WaitForAsync(game, () => game.IsBoardInputEnabled);
        Assert.True(game.IsBoardInputEnabled);
        Assert.Contains("/4p3/4P3/", new FenCodec().Format(game.PositionFacts));
    }

    [Fact]
    public async Task HumanBlackStartsFlippedAndEngineMovesFirst()
    {
        var opponent = new ControlledAnalyzer();
        await using var game = Create(
            Ai(SideDefinitions.Black),
            null,
            opponent);
        Assert.True(game.IsBoardFlipped);
        Assert.False(game.IsBoardInputEnabled);
        Assert.Single(opponent.Calls);
        game.FlipBoard();
        Assert.False(game.IsBoardFlipped);
        Assert.False(game.IsBoardInputEnabled);
        var move = new UciMoveCodec().Parse(Variant.CreateGame(), "e2e4");
        opponent.Complete(0, new AnalysisResult(SideDefinitions.White, move));
        await WaitForAsync(game, () => game.MoveCount == 1);
        await WaitForAsync(game, () => game.IsBoardInputEnabled);
        Assert.True(game.IsBoardInputEnabled);
        Assert.Equal("Black", game.CurrentTurn);
    }

    [Fact]
    public async Task NullBestMoveLeavesEngineSideLockedWithError()
    {
        var opponent = new ControlledAnalyzer();
        await using var game = Create(
            Ai(SideDefinitions.Black),
            null,
            opponent);
        opponent.Complete(0, new AnalysisResult(SideDefinitions.White, null));
        await WaitForAsync(game, () => game.OpponentError is not null);
        Assert.False(game.IsBoardInputEnabled);
        Assert.Equal(0, game.MoveCount);
        Assert.Equal("Unavailable", game.EngineStatus);
    }

    [Fact]
    public async Task DisposalCancelsActiveEvaluationAndOpponentMove()
    {
        var evaluation = new ControlledAnalyzer();
        var opponent = new ControlledAnalyzer(ignoreCancellation: true);
        var game = Create(Ai(SideDefinitions.Black), evaluation, opponent);
        Assert.Single(evaluation.Calls);
        Assert.Single(opponent.Calls);
        var dispose = game
            .DisposeAsync()
            .AsTask();
        Assert.True(evaluation.Calls[0].Canceled);
        Assert.True(opponent.Calls[0].Canceled);
        Assert.False(dispose.IsCompleted);
        var move = new UciMoveCodec().Parse(Variant.CreateGame(), "e2e4");
        opponent.Complete(0, new AnalysisResult(SideDefinitions.White, move));
        await dispose;
        Assert.Equal(0, game.MoveCount);
        Assert.True(evaluation.Disposed);
        Assert.True(opponent.Disposed);
    }

    [Fact]
    public async Task TerminalMoveStopsBothSearchesAndDisablesInput()
    {
        var evaluation = new ControlledAnalyzer();
        await using var game = Create(
            StandardSessionConfiguration.Local(),
            evaluation);
        ClickMove(game, "f2", "f3");
        ClickMove(game, "e7", "e5");
        ClickMove(game, "g2", "g4");
        Assert.Equal(4, evaluation.Calls.Count);
        ClickMove(game, "d8", "h4");
        Assert.Equal(4, evaluation.Calls.Count);
        Assert.False(game.IsBoardInputEnabled);
        Assert.Equal("0–1", game.EvaluationText);
        Assert.Equal(0, game.WhiteFraction);
        Assert.Equal("Checkmate", game.Status);
    }

    [Fact]
    public async Task EvaluationFailureDoesNotStopOpponent()
    {
        var evaluation = new ControlledAnalyzer();
        var opponent = new ControlledAnalyzer();
        await using var game = Create(
            Ai(SideDefinitions.Black),
            evaluation,
            opponent);
        evaluation.Fail(0, new IOException("search failed"));
        Assert.Equal("—", game.EvaluationText);
        Assert.False(game.IsEvaluationAvailable);
        Assert.NotNull(game.EvaluationError);
        var move = new UciMoveCodec().Parse(Variant.CreateGame(), "e2e4");
        opponent.Complete(0, new AnalysisResult(SideDefinitions.White, move));
        Assert.Equal(1, game.MoveCount);
        Assert.True(game.IsBoardInputEnabled);
    }

    [Fact]
    public async Task OpponentFailureKeepsEngineSideLocked()
    {
        var opponent = new ControlledAnalyzer();
        await using var game = Create(
            Ai(SideDefinitions.Black),
            null,
            opponent);
        opponent.Fail(0, new IOException("search failed"));
        Assert.NotNull(game.OpponentError);
        Assert.False(game.IsBoardInputEnabled);
        Assert.Equal(0, game.MoveCount);
    }

    private static GameViewModel Create(
        StandardSessionConfiguration configuration,
        ControlledAnalyzer? evaluation = null,
        ControlledAnalyzer? opponent = null)
    {
        var session = new StandardEngineSession(
            evaluation,
            evaluation,
            opponent,
            opponent);
        return new GameViewModel(
            Mode,
            configuration,
            session,
            () => Task.CompletedTask);
    }

    private static StandardSessionConfiguration Ai(
        Side side) =>
        new(
            StandardSessionType.PlayerVsStockfish,
            side,
            StandardDifficulty.Medium,
            "stockfish.exe");

    private static AnalysisResult Score(
        int cp,
        Side perspective) =>
        new(
            perspective,
            null,
            new AnalysisVariation(
                new CentipawnScore(cp),
                AnalysisScoreBound.Exact,
                []));

    private static void ClickMove(
        GameViewModel game,
        string from,
        string to)
    {
        var codec = new SquareCodec();
        game.SelectSquareCommand.Execute(
            game.Squares.Single(square => square.Square == codec.Parse(from)));
        game.SelectSquareCommand.Execute(
            game.Squares.Single(square => square.Square == codec.Parse(to)));
    }

    private static Task WaitForAsync(
        GameViewModel game,
        Func<bool> condition)
    {
        if (condition())
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void Changed(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if (!condition())
            {
                return;
            }

            game.PropertyChanged -= Changed;
            completion.TrySetResult();
        }

        game.PropertyChanged += Changed;
        if (condition())
        {
            game.PropertyChanged -= Changed;
            completion.TrySetResult();
        }

        return completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class ControlledAnalyzer :
        IPositionAnalyzer<StandardPositionFacts>, IAsyncDisposable
    {
        private readonly bool _ignoreCancellation;
        public List<Call> Calls { get; } = [];
        public bool Disposed { get; private set; }

        public ControlledAnalyzer(
            bool ignoreCancellation = false)
        {
            _ignoreCancellation = ignoreCancellation;
        }

        public Task<AnalysisResult> AnalyzeAsync(
            StandardPositionFacts position,
            AnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            var call = new Call(position, request);
            Calls.Add(call);
            cancellationToken.Register(() =>
            {
                call.Canceled = true;
                if (!_ignoreCancellation)
                {
                    call.Completion.TrySetCanceled(cancellationToken);
                }
            });
            return call.Completion.Task;
        }

        public void Complete(
            int index,
            AnalysisResult result) =>
            Calls[index]
                .Completion
                .TrySetResult(result);

        public void Fail(
            int index,
            Exception exception) =>
            Calls[index]
                .Completion
                .TrySetException(exception);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Call(
        StandardPositionFacts position,
        AnalysisRequest request)
    {
        public StandardPositionFacts Position { get; } = position;
        public AnalysisRequest Request { get; } = request;
        public TaskCompletionSource<AnalysisResult> Completion { get; } = new();
        public bool Canceled { get; set; }
    }
}
