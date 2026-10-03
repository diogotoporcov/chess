// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace Chess.Uci;

public sealed class UciEngineProcess : IAsyncDisposable
{
    private const int StandardErrorCapacity = 64;

    private readonly UciEngineProcessOptions _options;
    private readonly Process _process;
    private readonly Channel<string> _stdoutLines;
    private readonly Task _stdoutPump;
    private readonly Task _stderrPump;
    private readonly Task _processExit;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _searchLock = new();
    private readonly object _standardErrorLock = new();
    private readonly object _lifecycleLock = new();
    private readonly Queue<string> _standardErrorLines = new();
    private SearchState? _activeSearch;
    private Task? _shutdownTask;
    private Task? _disposeTask;
    private UciEngineException? _fault;
    private int _shutdownStarted;
    private int _disposed;

    public UciEngineInfo Info { get; private set; } = new(null, null, []);

    private UciEngineProcess(
        UciEngineProcessOptions options,
        Process process)
    {
        _options = options;
        _process = process;
        _stdoutLines = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions
            {
                SingleReader = true, SingleWriter = true
            });
        _processExit = process.WaitForExitAsync();
        _stdoutPump = PumpStandardOutputAsync();
        _stderrPump = PumpStandardErrorAsync();
    }

    public static async Task<UciEngineProcess> StartAsync(
        UciEngineProcessOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = CreateStartInfo(options);
        var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                process.Dispose();
                throw new UciEngineException(
                    "The UCI engine process could not be started.",
                    null,
                    []);
            }
        }
        catch
        {
            process.Dispose();
            throw;
        }

        process.StandardInput.NewLine = "\n";
        process.StandardInput.AutoFlush = true;

        var engine = new UciEngineProcess(options, process);
        try
        {
            await engine.InitializeAsync(cancellationToken);
            return engine;
        }
        catch
        {
            await engine.CleanupFailedStartupAsync();
            throw;
        }
    }

    public async Task WaitUntilReadyAsync(
        CancellationToken cancellationToken = default)
    {
        await ExecuteReadyBarrierAsync(["isready"], cancellationToken);
    }

    public async Task SetOptionAsync(
        string name,
        string? value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        UciProtocolText.ValidateSingleLine(name, nameof(name));
        if (value is not null)
        {
            UciProtocolText.ValidateSingleLine(value, nameof(value));
        }

        var command = value is null
            ? $"setoption name {name}"
            : $"setoption name {name} value {value}";

        await ExecuteReadyBarrierAsync([command, "isready"], cancellationToken);
    }

    public async Task NewGameAsync(
        CancellationToken cancellationToken = default)
    {
        await ExecuteReadyBarrierAsync(
            ["ucinewgame", "isready"],
            cancellationToken);
    }

    public async Task<UciSearchResult> SearchAsync(
        UciPosition position,
        UciGoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(command);
        ThrowIfUnavailable();

        await _operationGate.WaitAsync(cancellationToken);
        SearchState? search = null;
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();

            search = new SearchState();
            await _writeGate.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_lifecycleLock)
                {
                    ThrowIfUnavailable();
                    lock (_searchLock)
                    {
                        _activeSearch = search;
                    }
                }

                await WriteLineCoreAsync(position.ToProtocolCommand());
                await WriteLineCoreAsync(command.ToProtocolCommand());
            }
            finally
            {
                _writeGate.Release();
            }

            var infoLines = new List<string>();
            try
            {
                return await ReadSearchResultAsync(
                    infoLines,
                    null,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken
                                                         .IsCancellationRequested)
            {
                await RequestStopAsync(search, CancellationToken.None);
                try
                {
                    await ReadSearchResultAsync(
                        infoLines,
                        _options.ResponseTimeout,
                        CancellationToken.None);
                }
                catch (TimeoutException)
                {
                    await FaultAndKillAsync(
                        "The UCI engine did not produce bestmove after stop.");
                    throw;
                }
                catch (UciEngineException exception)
                {
                    await FaultAndKillAsync(exception);
                    throw;
                }

                throw new OperationCanceledException(cancellationToken);
            }
        }
        catch (UciEngineException exception)
        {
            await FaultAndKillAsync(exception);
            throw;
        }
        catch (Exception exception) when
            (exception is not OperationCanceledException
                 and not TimeoutException and not ObjectDisposedException)
        {
            var engineException = CreateProtocolException(
                "The UCI engine search failed.",
                exception);
            await FaultAndKillAsync(engineException);
            throw engineException;
        }
        finally
        {
            if (search is not null)
            {
                lock (_searchLock)
                {
                    if (ReferenceEquals(_activeSearch, search))
                    {
                        _activeSearch = null;
                    }
                }
            }

            _operationGate.Release();
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();

        SearchState? search;
        lock (_searchLock)
        {
            search = _activeSearch;
        }

        if (search is not null)
        {
            await RequestStopAsync(search, cancellationToken);
        }
    }

    public async Task QuitAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task shutdownTask;
        lock (_lifecycleLock)
        {
            _shutdownStarted = 1;
            _shutdownTask ??= ShutdownCoreAsync();
            shutdownTask = _shutdownTask;
        }

        await shutdownTask.WaitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        Task disposeTask;
        lock (_lifecycleLock)
        {
            _disposeTask ??= DisposeCoreAsync();
            disposeTask = _disposeTask;
        }

        return new ValueTask(disposeTask);
    }

    private static ProcessStartInfo CreateStartInfo(
        UciEngineProcessOptions options)
    {
        var utf8 = new UTF8Encoding(false);
        var startInfo = new ProcessStartInfo
        {
            FileName = options.FileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = utf8,
            StandardOutputEncoding = utf8,
            StandardErrorEncoding = utf8
        };

        if (options.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = options.WorkingDirectory;
        }

        foreach (var argument in options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private async Task InitializeAsync(
        CancellationToken cancellationToken)
    {
        await WriteLinesAsync(["uci"], cancellationToken);

        string? name = null;
        string? author = null;
        var optionLines = new List<string>();

        try
        {
            using var timeout = new CancellationTokenSource(
                _options.ResponseTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);

            while (true)
            {
                string line;
                try
                {
                    line = await ReadProtocolLineAsync(linked.Token);
                }
                catch (OperationCanceledException) when (cancellationToken
                    .IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException(
                        "The UCI engine did not complete initialization in time.");
                }

                if (line == "uciok")
                {
                    Info = new UciEngineInfo(name, author, optionLines);
                    return;
                }

                if (line.StartsWith("id name ", StringComparison.Ordinal))
                {
                    name = line[8..];
                }
                else if (line.StartsWith(
                             "id author ",
                             StringComparison.Ordinal))
                {
                    author = line[10..];
                }
                else if (line.StartsWith("option ", StringComparison.Ordinal))
                {
                    optionLines.Add(line);
                }
            }
        }
        catch (UciEngineException exception)
        {
            _fault = exception;
            throw;
        }
    }

    private async Task ExecuteReadyBarrierAsync(
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            await WriteLinesAsync(commands, cancellationToken);

            try
            {
                await WaitForMarkerAsync(
                    "readyok",
                    _options.ResponseTimeout,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken
                                                         .IsCancellationRequested)
            {
                try
                {
                    await WaitForMarkerAsync(
                        "readyok",
                        _options.ResponseTimeout,
                        CancellationToken.None);
                }
                catch (TimeoutException)
                {
                    await FaultAndKillAsync(
                        "The UCI engine did not resynchronize after cancellation.");
                    throw;
                }
                catch (UciEngineException exception)
                {
                    await FaultAndKillAsync(exception);
                    throw;
                }

                throw new OperationCanceledException(cancellationToken);
            }
            catch (TimeoutException)
            {
                await FaultAndKillAsync(
                    "The UCI engine did not produce readyok in time.");
                throw;
            }
            catch (UciEngineException exception)
            {
                await FaultAndKillAsync(exception);
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task WaitForMarkerAsync(
        string marker,
        TimeSpan timeoutValue,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(timeoutValue);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        while (true)
        {
            string line;
            try
            {
                line = await ReadProtocolLineAsync(linked.Token);
            }
            catch (OperationCanceledException) when (cancellationToken
                                                         .IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"The UCI engine did not produce {marker} in time.");
            }

            if (line == marker)
            {
                return;
            }
        }
    }

    private async Task<UciSearchResult> ReadSearchResultAsync(
        List<string> infoLines,
        TimeSpan? timeoutValue,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource? timeout = null;
        CancellationTokenSource? linked = null;
        var effectiveToken = cancellationToken;

        if (timeoutValue is not null)
        {
            timeout = new CancellationTokenSource(timeoutValue.Value);
            linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);
            effectiveToken = linked.Token;
        }

        try
        {
            while (true)
            {
                string line;
                try
                {
                    line = await ReadProtocolLineAsync(effectiveToken);
                }
                catch (OperationCanceledException) when (cancellationToken
                    .IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException(
                        "The UCI engine did not produce bestmove in time.");
                }

                if (line == "info" ||
                    line.StartsWith("info ", StringComparison.Ordinal))
                {
                    infoLines.Add(line);
                    continue;
                }

                if (line == "bestmove" ||
                    line.StartsWith("bestmove ", StringComparison.Ordinal))
                {
                    return ParseBestMove(line, infoLines);
                }
            }
        }
        finally
        {
            linked?.Dispose();
            timeout?.Dispose();
        }
    }

    private UciSearchResult ParseBestMove(
        string line,
        IReadOnlyList<string> infoLines)
    {
        var tokens = line.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 2)
        {
            return new UciSearchResult(tokens[1], null, infoLines);
        }

        if (tokens.Length == 4 &&
            tokens[2] == "ponder")
        {
            return new UciSearchResult(tokens[1], tokens[3], infoLines);
        }

        throw CreateProtocolException(
            $"Malformed UCI bestmove response: {line}");
    }

    private async Task RequestStopAsync(
        SearchState search,
        CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            lock (_searchLock)
            {
                if (!ReferenceEquals(_activeSearch, search) ||
                    search.StopSent)
                {
                    return;
                }

                search.StopSent = true;
            }

            await WriteLineCoreAsync("stop");
        }
        catch (Exception exception) when
            (exception is not OperationCanceledException)
        {
            var engineException = CreateProtocolException(
                "Failed to send stop to the UCI engine.",
                exception);
            await FaultAndKillAsync(engineException);
            throw engineException;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteLinesAsync(
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var line in lines)
            {
                await WriteLineCoreAsync(line);
            }
        }
        catch (Exception exception) when
            (exception is not OperationCanceledException)
        {
            var engineException = CreateProtocolException(
                "Failed to write to the UCI engine.",
                exception);
            await FaultAndKillAsync(engineException);
            throw engineException;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteLineCoreAsync(
        string line)
    {
        await _process.StandardInput.WriteLineAsync(line);
    }

    private async Task<string> ReadProtocolLineAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _stdoutLines.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException exception)
        {
            throw await CreateProcessFailureAsync(
                "The UCI engine closed standard output unexpectedly.",
                exception);
        }
    }

    private async Task PumpStandardOutputAsync()
    {
        Exception? failure = null;
        try
        {
            while (await _process.StandardOutput.ReadLineAsync() is { } line)
            {
                await _stdoutLines.Writer.WriteAsync(line);
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            _stdoutLines.Writer.TryComplete(failure);
        }
    }

    private async Task PumpStandardErrorAsync()
    {
        try
        {
            while (await _process.StandardError.ReadLineAsync() is { } line)
            {
                lock (_standardErrorLock)
                {
                    if (_standardErrorLines.Count == StandardErrorCapacity)
                    {
                        _standardErrorLines.Dequeue();
                    }

                    _standardErrorLines.Enqueue(line);
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
    }

    private async Task<UciEngineException> CreateProcessFailureAsync(
        string message,
        Exception? innerException = null)
    {
        if (!_process.HasExited)
        {
            TryKillProcess();
        }

        await ObserveProcessExitAndErrorPumpAsync();
        return new UciEngineException(
            message,
            GetExitCode(),
            GetStandardErrorSnapshot(),
            innerException);
    }

    private UciEngineException CreateProtocolException(
        string message,
        Exception? innerException = null)
    {
        return new UciEngineException(
            message,
            GetExitCode(),
            GetStandardErrorSnapshot(),
            innerException);
    }

    private async Task FaultAndKillAsync(
        string message)
    {
        await FaultAndKillAsync(CreateProtocolException(message));
    }

    private async Task FaultAndKillAsync(
        UciEngineException exception)
    {
        _fault ??= exception;
        TryKillProcess();
        await ObserveProcessExitAndPumpsAsync();
    }

    private async Task CleanupFailedStartupAsync()
    {
        _shutdownStarted = 1;
        TryKillProcess();
        await ObserveProcessExitAndPumpsAsync();
        await _process.StandardInput.DisposeAsync();
        _process.Dispose();
        _operationGate.Dispose();
        _writeGate.Dispose();
        _disposed = 1;
    }

    private async Task ShutdownCoreAsync()
    {
        SearchState? search;
        lock (_searchLock)
        {
            search = _activeSearch;
        }

        if (search is not null)
        {
            try
            {
                await RequestStopAsync(search, CancellationToken.None);
            }
            catch (UciEngineException)
            {
            }
        }

        var ownsOperationGate = false;
        try
        {
            ownsOperationGate = await _operationGate.WaitAsync(
                _options.ShutdownTimeout);
            if (!ownsOperationGate)
            {
                TryKillProcess();
                await ObserveProcessExitAndPumpsAsync();
                await _operationGate.WaitAsync();
                ownsOperationGate = true;
            }
            else if (!_process.HasExited)
            {
                try
                {
                    await WriteLinesAsync(["quit"], CancellationToken.None);
                }
                catch (UciEngineException)
                {
                    TryKillProcess();
                }
            }

            try
            {
                await _processExit.WaitAsync(_options.ShutdownTimeout);
            }
            catch (TimeoutException)
            {
                TryKillProcess();
                await _processExit;
            }

            await ObservePumpsAsync();
        }
        finally
        {
            if (ownsOperationGate)
            {
                _operationGate.Release();
            }
        }
    }

    private async Task DisposeCoreAsync()
    {
        Task shutdownTask;
        lock (_lifecycleLock)
        {
            _shutdownStarted = 1;
            _shutdownTask ??= ShutdownCoreAsync();
            shutdownTask = _shutdownTask;
        }

        try
        {
            await shutdownTask;
        }
        finally
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                await _operationGate.WaitAsync();
                await _writeGate.WaitAsync();
                await _process.StandardInput.DisposeAsync();
                _process.Dispose();
                _operationGate.Dispose();
                _writeGate.Dispose();
            }
        }
    }

    private async Task ObserveProcessExitAndPumpsAsync()
    {
        await ObserveProcessExitAndErrorPumpAsync();
        await ObserveTaskAsync(_stdoutPump);
    }

    private async Task ObserveProcessExitAndErrorPumpAsync()
    {
        await ObserveTaskAsync(_processExit);
        await ObserveTaskAsync(_stderrPump);
    }

    private async Task ObservePumpsAsync()
    {
        await ObserveTaskAsync(_stdoutPump);
        await ObserveTaskAsync(_stderrPump);
    }

    private static async Task ObserveTaskAsync(
        Task task)
    {
        await task;
    }

    private void TryKillProcess()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private int? GetExitCode()
    {
        try
        {
            return _process.HasExited ? _process.ExitCode : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private string[] GetStandardErrorSnapshot()
    {
        lock (_standardErrorLock)
        {
            return _standardErrorLines.ToArray();
        }
    }

    private void ThrowIfUnavailable()
    {
        if (Volatile.Read(ref _shutdownStarted) != 0 ||
            Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(UciEngineProcess));
        }

        if (_fault is not null)
        {
            throw new UciEngineException(
                "The UCI engine process is no longer usable.",
                _fault.ExitCode,
                _fault.StandardErrorLines,
                _fault);
        }
    }

    private sealed class SearchState
    {
        public bool StopSent { get; set; }
    }
}
