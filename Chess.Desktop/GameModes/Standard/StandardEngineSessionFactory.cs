// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Stockfish;
using Chess.Uci;

namespace Chess.Desktop.GameModes.Standard;

public interface IStandardEngineSessionFactory
{
    Task<IStandardEngineSession> StartAsync(
        StandardSessionConfiguration configuration,
        CancellationToken cancellationToken = default);
}

public sealed class StandardEngineSessionFactory : IStandardEngineSessionFactory
{
    private readonly Func<StockfishAnalyzerOptions, CancellationToken,
        Task<StandardAnalyzerResource>> _startAnalyzer;

    public StandardEngineSessionFactory() : this(StartStockfishAsync)
    {
    }

    public StandardEngineSessionFactory(
        Func<StockfishAnalyzerOptions, CancellationToken,
            Task<StandardAnalyzerResource>> startAnalyzer)
    {
        _startAnalyzer = startAnalyzer;
    }

    public async Task<IStandardEngineSession> StartAsync(
        StandardSessionConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.IsStockfishGame &&
            string.IsNullOrWhiteSpace(configuration.StockfishPath))
        {
            throw new ArgumentException(
                "Choose a Stockfish executable before starting.",
                nameof(configuration));
        }

        if (string.IsNullOrWhiteSpace(configuration.StockfishPath))
        {
            return new StandardEngineSession(null, null, null, null);
        }

        var processOptions = new UciEngineProcessOptions(
            configuration.StockfishPath.Trim());
        StandardAnalyzerResource? evaluation = null;
        StandardAnalyzerResource? opponent = null;
        try
        {
            evaluation = await _startAnalyzer(
                new StockfishAnalyzerOptions(processOptions, 1, 32),
                cancellationToken);
            if (configuration.IsStockfishGame)
            {
                opponent = await _startAnalyzer(
                    new StockfishAnalyzerOptions(
                        processOptions,
                        1,
                        32,
                        configuration.StrengthElo),
                    cancellationToken);
            }

            return new StandardEngineSession(
                evaluation.Analyzer,
                evaluation.Owner,
                opponent?.Analyzer,
                opponent?.Owner);
        }
        catch
        {
            if (opponent is not null)
            {
                await opponent.Owner.DisposeAsync();
            }

            if (evaluation is not null)
            {
                await evaluation.Owner.DisposeAsync();
            }

            throw;
        }
    }

    private static async Task<StandardAnalyzerResource> StartStockfishAsync(
        StockfishAnalyzerOptions options,
        CancellationToken cancellationToken)
    {
        var analyzer = await StockfishAnalyzer.StartAsync(
            options,
            cancellationToken);
        return new StandardAnalyzerResource(analyzer, analyzer);
    }
}
