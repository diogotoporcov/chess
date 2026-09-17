// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Core.Movement;
using Chess.Core.Notation;
using Chess.Variants.Standard.Movement;

namespace Chess.Variants.Standard.Notation.Uci;

public sealed class UciMoveCodec : IContextualNotationCodec<Game, Move>
{
    private static readonly SquareCodec SquareCodec = new();

    public Move Parse(
        Game game,
        string notation)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(notation);

        if (game.Status.IsTerminal)
        {
            throw new FormatException(
                "UCI notation cannot resolve a move after the game has ended.");
        }

        if (notation.Length is not (4 or 5))
        {
            throw new FormatException(
                "A Standard UCI move must contain four or five characters.");
        }

        var origin = SquareCodec.Parse(notation[..2]);
        var destination = SquareCodec.Parse(notation[2..4]);
        var promotion = notation.Length == 5
            ? ParsePromotion(notation[4])
            : null;
        var candidates = game
            .GenerateMoves(origin)
            .Where(candidate => candidate.To == destination)
            .Where(candidate => MatchesPromotion(candidate, promotion))
            .ToArray();

        if (candidates.Length != 1)
        {
            throw new FormatException(
                "UCI notation does not resolve to exactly one legal move.");
        }

        return candidates[0];
    }

    public string Format(
        Game game,
        Move move)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.Status.IsTerminal)
        {
            throw new InvalidOperationException(
                "Cannot format UCI notation after the game has ended.");
        }

        var topology = game.BoardState.Topology;

        if (!topology.Contains(move.From) ||
            !topology.Contains(move.To) ||
            !game
                .GenerateMoves(move.From)
                .Contains(move))
        {
            throw new ArgumentException(
                "Move is not a legal move in the current game state.",
                nameof(move));
        }

        var suffix = GetSuffix(move);

        return SquareCodec.Format(move.From) +
               SquareCodec.Format(move.To) +
               suffix;
    }

    private static bool MatchesPromotion(
        Move candidate,
        MoveOptionId? promotion)
    {
        return promotion is not null
            ? candidate.OptionId == promotion
            : IsSupportedSuffixlessOption(candidate.OptionId);
    }

    private static bool IsSupportedSuffixlessOption(
        MoveOptionId? option)
    {
        return option is null ||
               option == MoveOptions.EnPassant ||
               option == MoveOptions.CastleKingSide ||
               option == MoveOptions.CastleQueenSide;
    }

    private static MoveOptionId ParsePromotion(
        char suffix)
    {
        return suffix switch
        {
            'q' => PromotionOptions.Queen,
            'r' => PromotionOptions.Rook,
            'b' => PromotionOptions.Bishop,
            'n' => PromotionOptions.Knight,
            _ => throw new FormatException(
                "UCI notation has an invalid promotion suffix.")
        };
    }

    private static string GetSuffix(
        Move move)
    {
        if (move.OptionId is null ||
            move.OptionId == MoveOptions.EnPassant ||
            move.OptionId == MoveOptions.CastleKingSide ||
            move.OptionId == MoveOptions.CastleQueenSide)
        {
            return string.Empty;
        }

        if (move.OptionId == PromotionOptions.Queen)
        {
            return "q";
        }

        if (move.OptionId == PromotionOptions.Rook)
        {
            return "r";
        }

        if (move.OptionId == PromotionOptions.Bishop)
        {
            return "b";
        }

        if (move.OptionId == PromotionOptions.Knight)
        {
            return "n";
        }

        throw new ArgumentException(
            "Move has an unsupported Standard UCI option.",
            nameof(move));
    }
}
