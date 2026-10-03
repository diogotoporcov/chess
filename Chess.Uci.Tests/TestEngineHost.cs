// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Uci.TestEngine;

namespace Chess.Uci.Tests;

internal sealed class TestEngineHost : IAsyncDisposable
{
    private readonly string _directory;
    private readonly string _scenario;
    private UciEngineProcess? _engine;

    public string LogPath { get; }

    public string ReleasePath { get; }

    public TestEngineHost(
        string scenario)
    {
        _scenario = scenario;
        _directory = Path.Combine(
            Path.GetTempPath(),
            $"Chess.Uci.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        LogPath = Path.Combine(_directory, "commands.log");
        ReleasePath = Path.Combine(_directory, "release.signal");
    }

    public UciEngineProcessOptions CreateOptions(
        TimeSpan? responseTimeout = null,
        TimeSpan? shutdownTimeout = null)
    {
        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            hostPath = "dotnet";
        }

        var helperPath = typeof(TestEngineMarker).Assembly.Location;
        var testAssemblyPath = typeof(TestEngineHost).Assembly.Location;
        var runtimeConfigPath = Path.ChangeExtension(
            testAssemblyPath,
            ".runtimeconfig.json");

        return new UciEngineProcessOptions(
            hostPath,
            [
                "exec", "--runtimeconfig", runtimeConfigPath, helperPath,
                _scenario, LogPath, ReleasePath, _directory
            ],
            _directory,
            responseTimeout ?? TimeSpan.FromSeconds(5),
            shutdownTimeout ?? TimeSpan.FromMilliseconds(500));
    }

    public async Task<UciEngineProcess> StartAsync(
        TimeSpan? responseTimeout = null,
        TimeSpan? shutdownTimeout = null)
    {
        _engine = await UciEngineProcess.StartAsync(
            CreateOptions(responseTimeout, shutdownTimeout));
        return _engine;
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

        return lines.ToArray();
    }

    public async Task WaitForLogLineAsync(
        string expectedLine)
    {
        if (ReadLogLines()
            .Contains(expectedLine, StringComparer.Ordinal))
        {
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FileSystemWatcher(_directory);
        watcher.Filter = Path.GetFileName(LogPath);
        watcher.NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.Size;
        watcher.EnableRaisingEvents = true;

        void CheckLog(
            object? sender,
            FileSystemEventArgs arguments)
        {
            try
            {
                if (ReadLogLines()
                    .Contains(expectedLine, StringComparer.Ordinal))
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
        CheckLog(
            null,
            new FileSystemEventArgs(
                WatcherChangeTypes.All,
                _directory,
                Path.GetFileName(LogPath)));

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    public async ValueTask DisposeAsync()
    {
        if (_engine is not null)
        {
            await _engine.DisposeAsync();
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
