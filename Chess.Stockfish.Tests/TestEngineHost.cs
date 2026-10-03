// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Uci;
using Chess.Uci.TestEngine;

namespace Chess.Stockfish.Tests;

internal sealed class TestEngineHost : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"Chess.Stockfish.Tests-{Guid.NewGuid():N}");

    private readonly string _scenario;

    public string LogPath => Path.Combine(_directory, "commands.log");

    public string ReleasePath => Path.Combine(_directory, "release.signal");

    private string ResponsePath => Path.Combine(_directory, "response.txt");

    public TestEngineHost(
        string scenario = "stockfish normal")
    {
        _scenario = scenario;
        Directory.CreateDirectory(_directory);
    }

    public void SetResponse(
        params string[] lines)
    {
        File.WriteAllLines(ResponsePath, lines);
    }

    public UciEngineProcessOptions CreateProcessOptions()
    {
        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            hostPath = "dotnet";
        }

        return new UciEngineProcessOptions(
            hostPath,
            [
                "exec", "--runtimeconfig",
                Path.ChangeExtension(
                    typeof(TestEngineHost).Assembly.Location,
                    ".runtimeconfig.json"),
                typeof(TestEngineMarker).Assembly.Location, _scenario, LogPath,
                ReleasePath, _directory, ResponsePath
            ],
            _directory,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(500));
    }

    public StockfishAnalyzerOptions CreateOptions(
        int? threads = null,
        int? hashSizeMiB = null)
    {
        return new StockfishAnalyzerOptions(
            CreateProcessOptions(),
            threads,
            hashSizeMiB);
    }

    public string[] ReadLogLines()
    {
        if (!File.Exists(LogPath))
        {
            return [];
        }

        using var stream = new FileStream(
            LogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    public async Task WaitForLogLineAsync(
        string expected)
    {
        using var timeout =
            new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!ReadLogLines()
                   .Contains(expected, StringComparer.Ordinal))
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        Directory.Delete(_directory, true);
    }
}
