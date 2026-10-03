// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace Chess.Uci;

public sealed record UciGoCommand
{
    private readonly string _protocolCommand;

    private UciGoCommand(
        string protocolCommand)
    {
        _protocolCommand = protocolCommand;
    }

    public static UciGoCommand ByDepth(
        int depth)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depth),
                "Depth must be greater than zero.");
        }

        return new UciGoCommand(
            $"go depth {depth.ToString(CultureInfo.InvariantCulture)}");
    }

    public static UciGoCommand ByNodes(
        long nodes)
    {
        if (nodes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodes),
                "Nodes must be greater than zero.");
        }

        return new UciGoCommand(
            $"go nodes {nodes.ToString(CultureInfo.InvariantCulture)}");
    }

    public static UciGoCommand ByMoveTime(
        TimeSpan time)
    {
        if (time <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(time),
                "Move time must be greater than zero.");
        }

        var milliseconds = time.Ticks / TimeSpan.TicksPerMillisecond;
        if (time.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            milliseconds++;
        }

        if (milliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(time),
                "Move time must fit in a 32-bit millisecond value.");
        }

        return new UciGoCommand(
            $"go movetime {milliseconds.ToString(CultureInfo.InvariantCulture)}");
    }

    internal string ToProtocolCommand()
    {
        return _protocolCommand;
    }
}
