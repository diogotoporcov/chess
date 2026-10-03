// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Chess.Analysis;

namespace Chess.Stockfish;

internal static class StockfishInfoParser
{
    internal sealed record ParsedLine(
        int Rank,
        AnalysisScore Score,
        AnalysisScoreBound Bound,
        IReadOnlyList<string> PvTokens,
        int? Depth,
        int? SelectiveDepth,
        long? Nodes,
        TimeSpan? Elapsed);

    public static ParsedLine? Parse(
        string line)
    {
        var tokens = line.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 ||
            tokens[0] != "info" ||
            tokens.Length > 1 && tokens[1] == "string")
        {
            return null;
        }

        var pvIndex = Array.IndexOf(tokens, "pv");
        var end = pvIndex < 0 ? tokens.Length : pvIndex;
        var scoreIndex = Array.IndexOf(tokens, "score", 0, end);
        if (scoreIndex < 0)
        {
            return null;
        }

        if (pvIndex == tokens.Length - 1)
        {
            throw new StockfishException("Stockfish emitted an empty PV.");
        }

        if (scoreIndex + 2 >= end)
        {
            throw new StockfishException(
                "Stockfish emitted an incomplete score.");
        }

        var scoreValue = ParseInt(tokens[scoreIndex + 2], "score");
        AnalysisScore score = tokens[scoreIndex + 1] switch
        {
            "cp" => new CentipawnScore(scoreValue),
            "mate" => new MateScore(scoreValue),
            _ => throw new StockfishException(
                "Stockfish emitted an unknown score type.")
        };

        var lower = Array.IndexOf(tokens, "lowerbound", 0, end) >= 0;
        var upper = Array.IndexOf(tokens, "upperbound", 0, end) >= 0;
        if (lower && upper ||
            tokens
                .Take(end)
                .Count(token => token is "lowerbound" or "upperbound") >
            1)
        {
            throw new StockfishException(
                "Stockfish emitted conflicting score bounds.");
        }

        var bound = lower ? AnalysisScoreBound.Lower :
            upper ? AnalysisScoreBound.Upper : AnalysisScoreBound.Exact;
        var rank = ReadInt(tokens, end, "multipv") ?? 1;
        if (rank < 1)
        {
            throw new StockfishException(
                "Stockfish emitted an invalid MultiPV rank.");
        }

        var depth = ReadInt(tokens, end, "depth");
        var selectiveDepth = ReadInt(tokens, end, "seldepth");
        var nodes = ReadLong(tokens, end, "nodes");
        var time = ReadLong(tokens, end, "time");
        if (depth is < 0 ||
            selectiveDepth is < 0 ||
            nodes is < 0 ||
            time is < 0 ||
            time > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond)
        {
            throw new StockfishException(
                "Stockfish emitted negative or excessive analysis metadata.");
        }

        return new ParsedLine(
            rank,
            score,
            bound,
            pvIndex < 0 ? [] : tokens[(pvIndex + 1)..],
            depth,
            selectiveDepth,
            nodes,
            time is null
                ? null
                : TimeSpan.FromTicks(
                    time.Value * TimeSpan.TicksPerMillisecond));
    }

    private static int? ReadInt(
        string[] tokens,
        int end,
        string marker)
    {
        var index = Array.IndexOf(tokens, marker, 0, end);
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= end)
        {
            throw new StockfishException(
                $"Stockfish emitted incomplete {marker}.");
        }

        return ParseInt(tokens[index + 1], marker);
    }

    private static long? ReadLong(
        string[] tokens,
        int end,
        string marker)
    {
        var index = Array.IndexOf(tokens, marker, 0, end);
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= end ||
            !long.TryParse(
                tokens[index + 1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new StockfishException(
                $"Stockfish emitted invalid {marker}.");
        }

        return value;
    }

    private static int ParseInt(
        string token,
        string marker)
    {
        if (!int.TryParse(
                token,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new StockfishException(
                $"Stockfish emitted invalid {marker}.");
        }

        return value;
    }
}
