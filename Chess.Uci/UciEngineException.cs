// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;

namespace Chess.Uci;

public sealed class UciEngineException : Exception
{
    public int? ExitCode { get; }

    public IReadOnlyList<string> StandardErrorLines { get; }

    public UciEngineException(
        string message,
        int? exitCode,
        IEnumerable<string> standardErrorLines,
        Exception? innerException = null) : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(standardErrorLines);

        ExitCode = exitCode;
        StandardErrorLines = new ReadOnlyCollection<string>(
            standardErrorLines.ToArray());
    }
}
