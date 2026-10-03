// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Analysis;

public interface IPositionAnalyzer<TPosition> where TPosition : notnull
{
    Task<AnalysisResult> AnalyzeAsync(
        TPosition position,
        AnalysisRequest request,
        CancellationToken cancellationToken = default);
}
