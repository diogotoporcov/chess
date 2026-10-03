// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Analysis;
using Chess.Variants.Standard.Games.History;

namespace Chess.Desktop.GameModes.Standard;

public interface IStandardEngineSession : IAsyncDisposable
{
    IPositionAnalyzer<StandardPositionFacts>? EvaluationAnalyzer { get; }

    IPositionAnalyzer<StandardPositionFacts>? OpponentAnalyzer { get; }
}

public sealed class StandardEngineSession : IStandardEngineSession
{
    private readonly IAsyncDisposable? _evaluationOwner;
    private readonly IAsyncDisposable? _opponentOwner;

    public IPositionAnalyzer<StandardPositionFacts>? EvaluationAnalyzer { get; }

    public IPositionAnalyzer<StandardPositionFacts>? OpponentAnalyzer { get; }

    public StandardEngineSession(
        IPositionAnalyzer<StandardPositionFacts>? evaluationAnalyzer,
        IAsyncDisposable? evaluationOwner,
        IPositionAnalyzer<StandardPositionFacts>? opponentAnalyzer,
        IAsyncDisposable? opponentOwner)
    {
        EvaluationAnalyzer = evaluationAnalyzer;
        OpponentAnalyzer = opponentAnalyzer;
        _evaluationOwner = evaluationOwner;
        _opponentOwner = opponentOwner;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_opponentOwner is not null)
            {
                await _opponentOwner.DisposeAsync();
            }
        }
        finally
        {
            if (_evaluationOwner is not null)
            {
                await _evaluationOwner.DisposeAsync();
            }
        }
    }
}

public sealed record StandardAnalyzerResource(
    IPositionAnalyzer<StandardPositionFacts> Analyzer,
    IAsyncDisposable Owner);
