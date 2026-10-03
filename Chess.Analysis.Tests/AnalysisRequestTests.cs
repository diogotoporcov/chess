// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Analysis.Tests;

public sealed class AnalysisRequestTests
{
    [Fact]
    public void Constructor_DefaultsToOneVariation()
    {
        var limit = AnalysisLimit.ByDepth(10);

        var request = new AnalysisRequest(limit);

        Assert.Same(limit, request.Limit);
        Assert.Equal(1, request.VariationCount);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Constructor_PreservesCustomVariationCount(
        int variationCount)
    {
        var request = new AnalysisRequest(
            AnalysisLimit.ByNodes(1),
            variationCount);

        Assert.Equal(variationCount, request.VariationCount);
    }

    [Fact]
    public void Constructor_RejectsNullLimit()
    {
        Assert.Throws<ArgumentNullException>(() => new AnalysisRequest(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveVariationCount(
        int variationCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnalysisRequest(AnalysisLimit.ByDepth(1), variationCount));
    }
}
