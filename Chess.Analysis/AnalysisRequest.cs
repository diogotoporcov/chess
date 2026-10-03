// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Analysis;

public sealed record AnalysisRequest
{
    public AnalysisLimit Limit { get; }

    public int VariationCount { get; }

    public AnalysisRequest(
        AnalysisLimit limit,
        int variationCount = 1)
    {
        ArgumentNullException.ThrowIfNull(limit);

        if (variationCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(variationCount),
                "Variation count must be at least one.");
        }

        Limit = limit;
        VariationCount = variationCount;
    }
}
