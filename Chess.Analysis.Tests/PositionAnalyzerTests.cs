// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Sides;

namespace Chess.Analysis.Tests;

public sealed class PositionAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_IsConsumableThroughGenericAbstraction()
    {
        var position = new TestPosition("position-1");
        var request = new AnalysisRequest(AnalysisLimit.ByDepth(4), 2);
        var expected = new AnalysisResult(new Side("test:side"), null);
        IPositionAnalyzer<TestPosition> analyzer = new FakeAnalyzer(expected);
        using var cancellationSource = new CancellationTokenSource();

        var actual = await analyzer.AnalyzeAsync(
            position,
            request,
            cancellationSource.Token);
        var fake = Assert.IsType<FakeAnalyzer>(analyzer);
        var receivedPosition = Assert.IsType<TestPosition>(
            fake.ReceivedPosition);

        Assert.Same(expected, actual);
        Assert.Equal(position, receivedPosition);
        Assert.Equal("position-1", receivedPosition.Value);
        Assert.Same(request, fake.ReceivedRequest);
        Assert.Equal(cancellationSource.Token, fake.ReceivedCancellationToken);
    }

    [Fact]
    public async Task AnalyzeAsync_PropagatesPreCanceledToken()
    {
        var result = new AnalysisResult(new Side("test:side"), null);
        IPositionAnalyzer<TestPosition> analyzer = new FakeAnalyzer(result);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            analyzer.AnalyzeAsync(
                new TestPosition("position-1"),
                new AnalysisRequest(AnalysisLimit.ByNodes(1)),
                cancellationSource.Token));
    }

    private sealed record TestPosition(string Value);

    private sealed class FakeAnalyzer(AnalysisResult result)
        : IPositionAnalyzer<TestPosition>
    {
        public TestPosition? ReceivedPosition { get; private set; }

        public AnalysisRequest? ReceivedRequest { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<AnalysisResult> AnalyzeAsync(
            TestPosition position,
            AnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            ReceivedPosition = position;
            ReceivedRequest = request;
            ReceivedCancellationToken = cancellationToken;

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(result);
        }
    }
}
