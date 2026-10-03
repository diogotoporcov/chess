// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Uci.Tests;

public sealed class UciEngineCommandTests
{
    [Fact]
    public async Task SetOptionAsync_SendsValueAndReadinessBarrier()
    {
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();

        await engine.SetOptionAsync(
            "Hash",
            "256",
            TestContext.Current.CancellationToken);
        await engine.SetOptionAsync(
            "Skill Level",
            "Expert Mode",
            TestContext.Current.CancellationToken);

        var lines = host.ReadLogLines();
        Assert.Contains("IN setoption name Hash value 256", lines);
        Assert.Contains(
            "IN setoption name Skill Level value Expert Mode",
            lines);
        Assert.Equal(2, lines.Count(line => line == "IN isready"));
        Assert.Equal(2, lines.Count(line => line == "OUT readyok"));
    }

    [Fact]
    public async Task SetOptionAsync_SendsButtonAndReadinessBarrier()
    {
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();

        await engine.SetOptionAsync(
            "Clear Hash",
            null,
            TestContext.Current.CancellationToken);

        AssertCommandTail(host, "IN setoption name Clear Hash", "IN isready");
    }

    [Theory]
    [InlineData("Hash\rquit", "256")]
    [InlineData("Hash\nquit", "256")]
    [InlineData("Hash\0quit", "256")]
    [InlineData("Hash", "256\rquit")]
    [InlineData("Hash", "256\nquit")]
    [InlineData("Hash", "256\0quit")]
    public async Task SetOptionAsync_RejectsCommandInjection(
        string name,
        string value)
    {
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.SetOptionAsync(
                name,
                value,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NewGameAsync_SendsCommandAndReadinessBarrier()
    {
        await using var host = new TestEngineHost("normal scenario");
        await using var engine = await host.StartAsync();

        await engine.NewGameAsync(TestContext.Current.CancellationToken);

        AssertCommandTail(host, "IN ucinewgame", "IN isready");
    }

    private static void AssertCommandTail(
        TestEngineHost host,
        params string[] expected)
    {
        var inputs = host
            .ReadLogLines()
            .Where(line => line.StartsWith("IN ", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(expected, inputs[^expected.Length..]);
        Assert.Contains("OUT readyok", host.ReadLogLines());
    }
}
