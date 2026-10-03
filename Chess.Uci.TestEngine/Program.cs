// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;

namespace Chess.Uci.TestEngine;

internal static class Program
{
    public static async Task<int> Main(
        string[] arguments)
    {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);

        if (arguments.Length < 2)
        {
            return 2;
        }

        var scenario = arguments[0];
        var logPath = arguments[1];
        var releasePath = arguments.Length > 2 ? arguments[2] : null;
        var expectedWorkingDirectory = arguments.Length > 3
            ? arguments[3]
            : null;
        var responsePath = arguments.Length > 4 ? arguments[4] : null;
        var stockfishScenario = scenario.StartsWith(
            "stockfish",
            StringComparison.Ordinal);
        if (expectedWorkingDirectory is not null &&
            Path.GetFullPath(Environment.CurrentDirectory) !=
            Path.GetFullPath(expectedWorkingDirectory))
        {
            return 19;
        }

        var searchCount = 0;
        var waitingForStop = false;
        var multiPv = 1;

        while (await Console.In.ReadLineAsync() is { } command)
        {
            var loggedCommand = scenario == "pause search input" &&
                                command.StartsWith(
                                    "position ",
                                    StringComparison.Ordinal)
                ? "position startup-race"
                : command;
            await LogAsync(logPath, $"IN {loggedCommand}");

            if (command == "uci")
            {
                if (scenario == "exit before uciok")
                {
                    await Console.Error.WriteLineAsync("fatal-test-message");
                    await Console.Error.FlushAsync();
                    return 17;
                }

                if (scenario == "stderr flood")
                {
                    for (var index = 0; index < 10_000; index++)
                    {
                        await Console.Error.WriteLineAsync(
                            $"stderr-flood-{index:D5}-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
                    }

                    await Console.Error.FlushAsync();
                }

                if (scenario != "no uciok")
                {
                    await SendAsync(logPath, "fake engine banner");
                    await SendAsync(logPath, "info string initializing");
                    await SendAsync(logPath, "id name Chess Fake UCI Engine");
                    await SendAsync(logPath, "id author Chess.Tests");
                    if (scenario != "stockfish no hash")
                    {
                        await SendAsync(
                            logPath,
                            "option name Hash type spin default 16 min 1 max 1024");
                    }

                    await SendAsync(
                        logPath,
                        "option name Clear Hash type button");
                    if (stockfishScenario)
                    {
                        if (scenario != "stockfish no threads")
                        {
                            await SendAsync(
                                logPath,
                                "option name Threads type spin default 1 min 1 max 8");
                        }

                        if (scenario != "stockfish no multipv")
                        {
                            await SendAsync(
                                logPath,
                                scenario == "stockfish malformed multipv"
                                    ? "option name MultiPV type spin default 1 min nope max 5"
                                    : "option name MultiPV type spin default 1 min 1 max 5");
                        }

                        await SendAsync(
                            logPath,
                            "option name UCI_Chess960 type check default false");
                    }

                    await SendAsync(logPath, "uciok");
                    if (scenario == "pause search input" &&
                        releasePath is not null)
                    {
                        await LogAsync(logPath, "EVENT search-input-paused");
                        await WaitForFileAsync(releasePath);
                    }
                }

                continue;
            }

            if (command == "isready")
            {
                if (scenario == "exit on ready")
                {
                    await Console.Error.WriteLineAsync("fatal-ready-message");
                    await Console.Error.FlushAsync();
                    return 17;
                }

                if (scenario == "no readyok")
                {
                    continue;
                }

                if (scenario == "delayed ready")
                {
                    if (releasePath is not null)
                    {
                        await WaitForFileAsync(releasePath);
                    }
                }

                await SendAsync(logPath, "info string ready diagnostic");
                await SendAsync(logPath, "readyok");
                continue;
            }

            if (command.StartsWith("go ", StringComparison.Ordinal))
            {
                if (stockfishScenario)
                {
                    searchCount++;
                    if (scenario == "stockfish wait for stop" &&
                        searchCount == 1)
                    {
                        waitingForStop = true;
                        continue;
                    }

                    if (scenario == "stockfish held first" &&
                        searchCount == 1 &&
                        releasePath is not null)
                    {
                        await LogAsync(logPath, "EVENT first-search-held");
                        await WaitForFileAsync(releasePath);
                    }

                    if (responsePath is not null &&
                        File.Exists(responsePath))
                    {
                        foreach (var line in await File.ReadAllLinesAsync(
                                     responsePath))
                        {
                            await SendAsync(logPath, line);
                        }
                    }
                    else
                    {
                        for (var rank = 1; rank <= multiPv; rank++)
                        {
                            await SendAsync(
                                logPath,
                                $"info depth 1 multipv {rank} score cp {rank} pv e2e4");
                        }

                        await SendAsync(logPath, "bestmove e2e4");
                    }

                    continue;
                }

                if (scenario == "exit on search")
                {
                    await Console.Error.WriteLineAsync("fatal-search-message");
                    await Console.Error.FlushAsync();
                    return 17;
                }

                searchCount++;
                if (scenario == "wait for stop")
                {
                    if (searchCount == 1)
                    {
                        waitingForStop = true;
                    }
                    else
                    {
                        await SendAsync(logPath, "bestmove h2h3");
                    }

                    continue;
                }

                if (scenario is "ignore stop" or "pause search input")
                {
                    waitingForStop = true;
                    continue;
                }

                if (scenario == "malformed empty")
                {
                    await SendAsync(logPath, "bestmove");
                    continue;
                }

                if (scenario == "malformed ponder")
                {
                    await SendAsync(logPath, "bestmove e2e4 ponder");
                    continue;
                }

                if (scenario == "malformed trailing")
                {
                    await SendAsync(logPath, "bestmove e2e4 nonsense e7e5");
                    continue;
                }

                if (scenario == "sentinel zero")
                {
                    await SendAsync(logPath, "bestmove 0000");
                    continue;
                }

                if (scenario == "sentinel none")
                {
                    await SendAsync(logPath, "bestmove (none)");
                    continue;
                }

                if (scenario == "serial searches")
                {
                    await SendAsync(
                        logPath,
                        searchCount == 1 ? "bestmove a2a3" : "bestmove h2h3");
                    continue;
                }

                await SendAsync(logPath, "info depth 1 nodes 20 pv e2e4");
                await SendAsync(logPath, "info string deterministic result");
                await SendAsync(logPath, "bestmove e2e4 ponder e7e5");
                continue;
            }

            if (command == "stop" && waitingForStop)
            {
                if (stockfishScenario)
                {
                    waitingForStop = false;
                    await SendAsync(logPath, "bestmove e2e4");
                    continue;
                }

                if (scenario == "ignore stop")
                {
                    continue;
                }

                waitingForStop = false;
                await SendAsync(logPath, "bestmove a2a3");
                continue;
            }

            if (command == "quit")
            {
                if (scenario == "ignore quit")
                {
                    continue;
                }

                await LogAsync(logPath, "EVENT graceful-exit");
                return 0;
            }

            if (stockfishScenario &&
                command.StartsWith(
                    "setoption name MultiPV value ",
                    StringComparison.Ordinal) &&
                int.TryParse(
                    command["setoption name MultiPV value ".Length..],
                    out var requestedMultiPv))
            {
                multiPv = requestedMultiPv;
            }
        }

        return 0;
    }

    private static async Task WaitForFileAsync(
        string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FileSystemWatcher(
            Path.GetDirectoryName(path)!,
            Path.GetFileName(path));
        watcher.Created += (_, _) => completion.TrySetResult();
        watcher.EnableRaisingEvents = true;
        if (File.Exists(path))
        {
            return;
        }

        await completion.Task;
    }

    private static async Task SendAsync(
        string logPath,
        string line)
    {
        await LogAsync(logPath, $"OUT {line}");
        await Console.Out.WriteLineAsync(line);
        await Console.Out.FlushAsync();
    }

    private static async Task LogAsync(
        string logPath,
        string line)
    {
        await using var stream = new FileStream(
            logPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            true);
        await using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(false));
        await writer.WriteLineAsync(line);
    }
}
