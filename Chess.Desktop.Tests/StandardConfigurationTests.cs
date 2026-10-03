// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Analysis;
using Chess.Desktop.GameModes.Standard;
using Chess.Stockfish;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Sides;

namespace Chess.Desktop.Tests;

public sealed class StandardConfigurationTests
{
    [Theory]
    [InlineData(StandardDifficulty.Beginner, 1320)]
    [InlineData(StandardDifficulty.Easy, 1500)]
    [InlineData(StandardDifficulty.Medium, 1800)]
    [InlineData(StandardDifficulty.Hard, 2200)]
    [InlineData(StandardDifficulty.Expert, 2600)]
    [InlineData(StandardDifficulty.Maximum, null)]
    public void DifficultyMapsToEngineElo(
        StandardDifficulty difficulty,
        int? elo)
    {
        Assert.Equal(
            elo,
            new StandardSessionConfiguration(
                StandardSessionType.PlayerVsStockfish,
                SideDefinitions.White,
                difficulty,
                "stockfish.exe").StrengthElo);
    }

    [Fact]
    public async Task ProcessCountsAndFullStrengthEvaluation()
    {
        var options = new List<StockfishAnalyzerOptions>();
        var resources = new List<TestResource>();
        var factory = new StandardEngineSessionFactory((value, _) =>
        {
            options.Add(value);
            var resource = new TestResource();
            resources.Add(resource);
            return Task.FromResult(
                new StandardAnalyzerResource(resource, resource));
        });
        await using (var empty = await factory.StartAsync(
                         StandardSessionConfiguration.Local(),
                         TestContext.Current.CancellationToken))
        {
            Assert.Null(empty.EvaluationAnalyzer);
            Assert.Empty(options);
        }

        await using (var local = await factory.StartAsync(
                         StandardSessionConfiguration.Local("stockfish.exe"),
                         TestContext.Current.CancellationToken))
        {
            Assert.NotNull(local.EvaluationAnalyzer);
            Assert.Null(local.OpponentAnalyzer);
            Assert.Single(options);
            Assert.Null(options[0].StrengthElo);
        }

        await using (var ai = await factory.StartAsync(
                         new StandardSessionConfiguration(
                             StandardSessionType.PlayerVsStockfish,
                             SideDefinitions.White,
                             StandardDifficulty.Beginner,
                             "stockfish.exe"),
                         TestContext.Current.CancellationToken))
        {
            Assert.NotSame(ai.EvaluationAnalyzer, ai.OpponentAnalyzer);
            Assert.Equal(3, options.Count);
            Assert.Null(options[1].StrengthElo);
            Assert.Equal(1320, options[2].StrengthElo);
        }

        await using (await factory.StartAsync(
                         new StandardSessionConfiguration(
                             StandardSessionType.PlayerVsStockfish,
                             SideDefinitions.White,
                             StandardDifficulty.Maximum,
                             "stockfish.exe"),
                         TestContext.Current.CancellationToken))
        {
            Assert.Null(options[3].StrengthElo);
            Assert.Null(options[4].StrengthElo);
        }

        Assert.All(resources, resource => Assert.True(resource.Disposed));
    }

    [Fact]
    public async Task PartialStartupFailureDisposesFirstResource()
    {
        var first = new TestResource();
        var calls = 0;
        var factory = new StandardEngineSessionFactory((_, _) =>
        {
            calls++;
            return calls == 1
                ? Task.FromResult(new StandardAnalyzerResource(first, first))
                : throw new StockfishException("Second process failed.");
        });
        await Assert.ThrowsAsync<StockfishException>(() => factory.StartAsync(
            new StandardSessionConfiguration(
                StandardSessionType.PlayerVsStockfish,
                SideDefinitions.White,
                StandardDifficulty.Medium,
                "stockfish.exe"),
            TestContext.Current.CancellationToken));
        Assert.True(first.Disposed);
    }

    private sealed class TestResource :
        IPositionAnalyzer<StandardPositionFacts>, IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public Task<AnalysisResult> AnalyzeAsync(
            StandardPositionFacts position,
            AnalysisRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
