// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Uci;
using Chess.Uci.TestEngine;

namespace Chess.Stockfish.Tests;

internal sealed class TestEngineHost : IAsyncDisposable
{
    private static readonly TimeSpan DefaultTestTimeout =
        TimeSpan.FromSeconds(30);

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

    public UciEngineProcessOptions CreateProcessOptions(
        TimeSpan? responseTimeout = null,
        TimeSpan? shutdownTimeout = null)
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
            responseTimeout ?? DefaultTestTimeout,
            shutdownTimeout ?? TimeSpan.FromMilliseconds(500));
    }

    public StockfishAnalyzerOptions CreateOptions(
        int? threads = null,
        int? hashSizeMiB = null,
        int? strengthElo = null,
        TimeSpan? responseTimeout = null,
        TimeSpan? shutdownTimeout = null)
    {
        return new StockfishAnalyzerOptions(
            CreateProcessOptions(responseTimeout, shutdownTimeout),
            threads,
            hashSizeMiB,
            strengthElo);
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
        await WaitForLogLineCountAsync(expected, 1);
    }

    public async Task WaitForLogLineCountAsync(
        string expected,
        int count)
    {
        if (ReadLogLines()
                .Count(line => line == expected) >=
            count)
        {
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var watcher = new FileSystemWatcher(_directory);
        using (watcher)
        {
            watcher.Filter = Path.GetFileName(LogPath);
            watcher.NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.LastWrite |
                                   NotifyFilters.Size;

            void CheckLog(
                object? sender,
                FileSystemEventArgs arguments)
            {
                try
                {
                    if (ReadLogLines()
                            .Count(line => line == expected) >=
                        count)
                    {
                        completion.TrySetResult();
                    }
                }
                catch (IOException)
                {
                }
            }

            watcher.Changed += CheckLog;
            watcher.Created += CheckLog;
            watcher.EnableRaisingEvents = true;
            CheckLog(
                null,
                new FileSystemEventArgs(
                    WatcherChangeTypes.All,
                    _directory,
                    Path.GetFileName(LogPath)));
            await completion.Task.WaitAsync(DefaultTestTimeout);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        Directory.Delete(_directory, true);
    }
}
