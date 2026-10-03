// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Chess.Analysis;
using Chess.Core.Games;
using Chess.Core.Movement;
using Chess.Uci;
using Chess.Variants.Standard;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Notation.Fen;
using Chess.Variants.Standard.Notation.Uci;

namespace Chess.Stockfish;

public sealed class StockfishAnalyzer :
    IPositionAnalyzer<StandardPositionFacts>, IAsyncDisposable
{
    private readonly UciEngineProcess _engine;
    private readonly StockfishSpinOption _multiPv;
    private readonly SemaphoreSlim _analysisGate = new(1, 1);
    private readonly FenCodec _fenCodec = new();
    private readonly UciMoveCodec _moveCodec = new();
    private int _disposed;

    private StockfishAnalyzer(
        UciEngineProcess engine,
        StockfishSpinOption multiPv)
    {
        _engine = engine;
        _multiPv = multiPv;
    }

    public static async Task<StockfishAnalyzer> StartAsync(
        StockfishAnalyzerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var engine = await UciEngineProcess.StartAsync(
            options.ProcessOptions,
            cancellationToken);
        try
        {
            var multiPv = StockfishSpinOption.Require(
                engine.Info.OptionLines,
                "MultiPV");
            StockfishSpinOption? threads = options.Threads is null
                ? null
                : StockfishSpinOption.Require(
                    engine.Info.OptionLines,
                    "Threads");
            StockfishSpinOption? hash = options.HashSizeMiB is null
                ? null
                : StockfishSpinOption.Require(engine.Info.OptionLines, "Hash");
            if (options.Threads is { } threadCount)
            {
                threads!.Value.Validate(threadCount, nameof(options.Threads));
            }

            if (options.HashSizeMiB is { } hashSize)
            {
                hash!.Value.Validate(hashSize, nameof(options.HashSizeMiB));
            }

            await engine.SetOptionAsync(
                "UCI_Chess960",
                "false",
                cancellationToken);
            if (options.Threads is { } configuredThreads)
            {
                await engine.SetOptionAsync(
                    "Threads",
                    configuredThreads.ToString(CultureInfo.InvariantCulture),
                    cancellationToken);
            }

            if (options.HashSizeMiB is { } configuredHash)
            {
                await engine.SetOptionAsync(
                    "Hash",
                    configuredHash.ToString(CultureInfo.InvariantCulture),
                    cancellationToken);
            }

            return new StockfishAnalyzer(engine, multiPv);
        }
        catch
        {
            await engine.DisposeAsync();
            throw;
        }
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        StandardPositionFacts position,
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        await _analysisGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            _multiPv.Validate(request.VariationCount, nameof(request));
            await _engine.SetOptionAsync(
                "MultiPV",
                request.VariationCount.ToString(CultureInfo.InvariantCulture),
                cancellationToken);

            var fen = _fenCodec.Format(position);
            var initialState = _fenCodec.Parse(fen);
            var search = await _engine.SearchAsync(
                UciPosition.FromFen(fen),
                MapLimit(request.Limit),
                cancellationToken);
            return Translate(initialState, search);
        }
        finally
        {
            _analysisGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _engine.DisposeAsync();
    }

    private AnalysisResult Translate(
        StandardInitialState initialState,
        UciSearchResult search)
    {
        var latest =
            new SortedDictionary<int, StockfishInfoParser.ParsedLine>();
        foreach (var line in search.InfoLines)
        {
            if (StockfishInfoParser.Parse(line) is { } parsed)
            {
                latest[parsed.Rank] = parsed;
            }
        }

        var variations = latest
            .Values
            .Select(line => new AnalysisVariation(
                line.Score,
                line.Bound,
                ParsePv(initialState, line.PvTokens),
                line.Depth,
                line.SelectiveDepth,
                line.Nodes,
                line.Elapsed))
            .ToArray();
        Move? bestMove = search.BestMove is "0000" or "(none)"
            ? null
            : ParseMove(
                Variant.CreateGame(initialState),
                search.BestMove,
                "bestmove");
        return new AnalysisResult(
            initialState.SideToMove,
            bestMove,
            variations);
    }

    private List<Move> ParsePv(
        StandardInitialState initialState,
        IReadOnlyList<string> tokens)
    {
        var game = Variant.CreateGame(initialState);
        var moves = new List<Move>(tokens.Count);
        foreach (var token in tokens)
        {
            var move = ParseMove(game, token, "PV");
            try
            {
                game.Execute(move);
            }
            catch (Exception exception) when (exception is not
                                                  OperationCanceledException)
            {
                throw new StockfishException(
                    $"Stockfish emitted an invalid PV move '{token}'.",
                    exception);
            }

            moves.Add(move);
        }

        return moves;
    }

    private Move ParseMove(
        Game game,
        string token,
        string source)
    {
        try
        {
            return _moveCodec.Parse(game, token);
        }
        catch (FormatException exception)
        {
            throw new StockfishException(
                $"Stockfish emitted an invalid {source} move '{token}'.",
                exception);
        }
    }

    private static UciGoCommand MapLimit(
        AnalysisLimit limit)
    {
        if (limit.Depth is { } depth)
        {
            return UciGoCommand.ByDepth(depth);
        }

        if (limit.Nodes is { } nodes)
        {
            return UciGoCommand.ByNodes(nodes);
        }

        if (limit.Time is { } time)
        {
            return UciGoCommand.ByMoveTime(time);
        }

        throw new InvalidOperationException(
            "Analysis limit has no supported value.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}
