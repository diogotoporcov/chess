// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Sides;
using Chess.Variants.Standard.Sides;

namespace Chess.Desktop.GameModes.Standard;

public enum StandardSessionType
{
    PlayerVsPlayer,
    PlayerVsStockfish
}

public enum StandardDifficulty
{
    Beginner,
    Easy,
    Medium,
    Hard,
    Expert,
    Maximum
}

public sealed record StandardSessionConfiguration(
    StandardSessionType SessionType,
    Side HumanSide,
    StandardDifficulty Difficulty,
    string StockfishPath)
{
    public bool IsStockfishGame =>
        SessionType == StandardSessionType.PlayerVsStockfish;

    public int? StrengthElo =>
        Difficulty switch
        {
            StandardDifficulty.Beginner => 1320,
            StandardDifficulty.Easy => 1500,
            StandardDifficulty.Medium => 1800,
            StandardDifficulty.Hard => 2200,
            StandardDifficulty.Expert => 2600,
            StandardDifficulty.Maximum => null,
            _ => throw new ArgumentOutOfRangeException(nameof(Difficulty))
        };

    public static StandardSessionConfiguration Local(
        string path = "") =>
        new(
            StandardSessionType.PlayerVsPlayer,
            SideDefinitions.White,
            StandardDifficulty.Medium,
            path);
}
