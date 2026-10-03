// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;

namespace Chess.Uci;

public sealed record UciEngineProcessOptions
{
    public static readonly TimeSpan DefaultResponseTimeout =
        TimeSpan.FromSeconds(10);

    public static readonly TimeSpan DefaultShutdownTimeout =
        TimeSpan.FromSeconds(2);

    public string FileName { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string? WorkingDirectory { get; }

    public TimeSpan ResponseTimeout { get; }

    public TimeSpan ShutdownTimeout { get; }

    public UciEngineProcessOptions(
        string fileName,
        IEnumerable<string>? arguments = null,
        string? workingDirectory = null,
        TimeSpan? responseTimeout = null,
        TimeSpan? shutdownTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var nullableArguments = arguments
            ?.Cast<string?>()
            .ToArray() ?? [];
        if (nullableArguments.Any(argument => argument is null))
        {
            throw new ArgumentException(
                "Arguments must not contain null entries.",
                nameof(arguments));
        }

        var argumentArray = nullableArguments
            .Select(argument => argument!)
            .ToArray();

        var actualResponseTimeout = responseTimeout ?? DefaultResponseTimeout;
        if (actualResponseTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseTimeout),
                "Response timeout must be greater than zero.");
        }

        var actualShutdownTimeout = shutdownTimeout ?? DefaultShutdownTimeout;
        if (actualShutdownTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shutdownTimeout),
                "Shutdown timeout must be greater than zero.");
        }

        FileName = fileName;
        Arguments = new ReadOnlyCollection<string>(argumentArray);
        WorkingDirectory = workingDirectory;
        ResponseTimeout = actualResponseTimeout;
        ShutdownTimeout = actualShutdownTimeout;
    }
}
