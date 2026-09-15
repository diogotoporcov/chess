// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using Chess.Core.Board;
using Chess.Core.Games;
using Chess.Core.Games.Status;
using Chess.Core.Movement;
using Chess.Core.Notation;
using Chess.Core.Pieces;
using Chess.Variants.Standard.Board;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Pieces;

namespace Chess.Variants.Standard.Notation.San;

public sealed class SanCodec : IContextualNotationCodec<Game, Move>
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
                "SAN cannot resolve a move after the game has ended.");
        }

        var syntax = ParseSyntax(notation);
        var moves = GetLegalMoves(game);
        var candidates = moves
            .Where(move => Matches(game, move, syntax))
            .ToArray();

        if (candidates.Length != 1)
        {
            throw new FormatException(
                "SAN does not resolve to exactly one legal move.");
        }

        var move = candidates[0];

        if (!StringComparer.Ordinal.Equals(Format(game, move), notation))
        {
            throw new FormatException("SAN is not in canonical form.");
        }

        return move;
    }

    public string Format(
        Game game,
        Move move)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.Status.IsTerminal)
        {
            throw new InvalidOperationException(
                "Cannot format SAN after the game has ended.");
        }

        if (!GetLegalMoves(game)
                .Contains(move))
        {
            throw new ArgumentException(
                "Move is not a legal move in the current game state.",
                nameof(move));
        }

        var basic = FormatBasic(game, move);
        game.Execute(move);

        try
        {
            return basic + GetSuffix(game.Status.Id);
        }
        finally
        {
            game.UndoLastMove();
        }
    }

    private static ParsedSan ParseSyntax(
        string notation)
    {
        if (notation.Length == 0)
        {
            throw new FormatException("SAN cannot be empty.");
        }

        var body = notation;

        if (body[^1] is '+' or '#')
        {
            body = body[..^1];
        }

        if (body.Length == 0 ||
            body.Any(character => character is '+' or '#'))
        {
            throw new FormatException("SAN has an invalid check suffix.");
        }

        if (body is "O-O" or "O-O-O")
        {
            return new ParsedSan(
                body == "O-O"
                    ? MoveOptions.CastleKingSide
                    : MoveOptions.CastleQueenSide,
                null,
                null,
                null,
                false,
                default,
                null);
        }

        MoveOptionId? promotion = null;

        if (body.Length >= 2 &&
            body[^2] == '=')
        {
            promotion = ParsePromotion(body[^1]);
            body = body[..^2];
        }
        else if (body.Contains('='))
        {
            throw new FormatException("SAN has an invalid promotion suffix.");
        }

        if (body.Length < 2)
        {
            throw new FormatException("SAN requires a destination square.");
        }

        Square destination;

        try
        {
            destination = SquareCodec.Parse(body[^2..]);
        }
        catch (FormatException exception)
        {
            throw new FormatException(
                "SAN has an invalid destination square.",
                exception);
        }

        var prefix = body[..^2];
        PieceDefinition? definition = null;

        if (prefix.Length > 0 &&
            prefix[0] is 'K' or 'Q' or 'R' or 'B' or 'N')
        {
            definition = ParsePiece(prefix[0]);
            prefix = prefix[1..];
        }

        var isCapture = prefix.EndsWith('x');

        if (prefix.Count(character => character == 'x') != (isCapture ? 1 : 0))
        {
            throw new FormatException("SAN has an invalid capture marker.");
        }

        if (isCapture)
        {
            prefix = prefix[..^1];
        }

        char? originFile = null;
        char? originRank = null;

        if (definition is null)
        {
            if (isCapture &&
                prefix.Length == 1 &&
                prefix[0] is >= 'a' and <= 'h')
            {
                originFile = prefix[0];
            }
            else if (!isCapture &&
                     prefix.Length == 0)
            {
            }
            else
            {
                throw new FormatException(
                    "SAN has an invalid pawn move prefix.");
            }
        }
        else
        {
            if (prefix.Length is > 2 ||
                prefix.Any(character =>
                    character is not ((>= 'a' and <= 'h')
                        or (>= '1' and <= '8'))))
            {
                throw new FormatException(
                    "SAN has an invalid origin disambiguation.");
            }

            if (prefix.Length >= 1)
            {
                if (prefix[0] is >= 'a' and <= 'h')
                {
                    originFile = prefix[0];
                }
                else
                {
                    originRank = prefix[0];
                }
            }

            if (prefix.Length == 2)
            {
                if (originFile is null ||
                    prefix[1] is < '1' or > '8')
                {
                    throw new FormatException(
                        "SAN has an invalid origin disambiguation.");
                }

                originRank = prefix[1];
            }
        }

        return new ParsedSan(
            null,
            definition,
            originFile,
            originRank,
            isCapture,
            destination,
            promotion);
    }

    private static bool Matches(
        Game game,
        Move move,
        ParsedSan syntax)
    {
        if (syntax.CastlingOption is not null)
        {
            return move.OptionId == syntax.CastlingOption;
        }

        if (move.To != syntax.Destination ||
            !MatchesPromotion(move.OptionId, syntax.PromotionOption) ||
            !game.BoardState.TryGetPiece(move.From, out var piece) ||
            !ReferenceEquals(
                piece.Definition,
                syntax.Definition ?? PieceDefinitions.Pawn) ||
            IsCapture(game, move) != syntax.Captures)
        {
            return false;
        }

        var column = BoardLayout.GetColumn(move.From);
        var row = BoardLayout.GetRow(move.From);

        return (syntax.OriginFile is null ||
                column == syntax.OriginFile - 'a') &&
               (syntax.OriginRank is null || row == '8' - syntax.OriginRank);
    }

    private static string FormatBasic(
        Game game,
        Move move)
    {
        if (move.OptionId == MoveOptions.CastleKingSide)
        {
            return "O-O";
        }

        if (move.OptionId == MoveOptions.CastleQueenSide)
        {
            return "O-O-O";
        }

        if (!game.BoardState.TryGetPiece(move.From, out var piece))
        {
            throw new ArgumentException(
                "Move origin does not contain a piece.",
                nameof(move));
        }

        var isPawn = ReferenceEquals(piece.Definition, PieceDefinitions.Pawn);
        var builder = new StringBuilder();

        if (!isPawn)
        {
            builder.Append(GetPieceSymbol(piece.Definition));
            builder.Append(GetDisambiguation(game, move, piece.Definition));
        }
        else if (IsCapture(game, move))
        {
            builder.Append((char)('a' + BoardLayout.GetColumn(move.From)));
        }

        if (IsCapture(game, move))
        {
            builder.Append('x');
        }

        builder.Append(SquareCodec.Format(move.To));

        if (move.OptionId is not null &&
            PromotionOptions.Contains(move.OptionId))
        {
            builder.Append('=');
            builder.Append(GetPromotionSymbol(move.OptionId));
        }

        return builder.ToString();
    }

    private static string GetDisambiguation(
        Game game,
        Move move,
        PieceDefinition definition)
    {
        var competitors = GetLegalMoves(game)
            .Where(candidate => candidate != move && candidate.To == move.To)
            .Where(candidate =>
                game.BoardState.TryGetPiece(candidate.From, out var piece) &&
                ReferenceEquals(piece.Definition, definition))
            .ToArray();

        if (competitors.Length == 0)
        {
            return string.Empty;
        }

        var column = BoardLayout.GetColumn(move.From);
        var row = BoardLayout.GetRow(move.From);
        var fileDistinguishes = competitors.All(candidate =>
            BoardLayout.GetColumn(candidate.From) != column);

        if (fileDistinguishes)
        {
            return ((char)('a' + column)).ToString();
        }

        var rankDistinguishes = competitors.All(candidate =>
            BoardLayout.GetRow(candidate.From) != row);

        if (rankDistinguishes)
        {
            return ((char)('8' - row)).ToString();
        }

        return SquareCodec.Format(move.From);
    }

    private static bool IsCapture(
        Game game,
        Move move)
    {
        return move.OptionId == MoveOptions.EnPassant ||
               game.BoardState.IsOccupied(move.To);
    }

    private static bool MatchesPromotion(
        MoveOptionId? moveOption,
        MoveOptionId? promotionOption)
    {
        if (promotionOption is not null)
        {
            return moveOption == promotionOption;
        }

        return moveOption is null || moveOption == MoveOptions.EnPassant;
    }

    private static IReadOnlyList<Move> GetLegalMoves(
        Game game)
    {
        return
        [
            .. game
                .BoardState
                .GetPiecePositions(game.State.CurrentSide)
                .SelectMany(position => game.GenerateMoves(position.Square))
        ];
    }

    private static PieceDefinition ParsePiece(
        char symbol)
    {
        return symbol switch
        {
            'K' => PieceDefinitions.King,
            'Q' => PieceDefinitions.Queen,
            'R' => PieceDefinitions.Rook,
            'B' => PieceDefinitions.Bishop,
            'N' => PieceDefinitions.Knight,
            _ => throw new FormatException(
                "SAN has an unsupported piece symbol.")
        };
    }

    private static char GetPieceSymbol(
        PieceDefinition definition)
    {
        if (ReferenceEquals(definition, PieceDefinitions.Knight))
        {
            return 'N';
        }

        if (ReferenceEquals(definition, PieceDefinitions.Bishop))
        {
            return 'B';
        }

        if (ReferenceEquals(definition, PieceDefinitions.Rook))
        {
            return 'R';
        }

        if (ReferenceEquals(definition, PieceDefinitions.Queen))
        {
            return 'Q';
        }

        if (ReferenceEquals(definition, PieceDefinitions.King))
        {
            return 'K';
        }

        throw new InvalidOperationException(
            "SAN does not support this piece definition.");
    }

    private static MoveOptionId ParsePromotion(
        char symbol)
    {
        return symbol switch
        {
            'Q' => PromotionOptions.Queen,
            'R' => PromotionOptions.Rook,
            'B' => PromotionOptions.Bishop,
            'N' => PromotionOptions.Knight,
            _ => throw new FormatException(
                "SAN has an invalid promotion piece.")
        };
    }

    private static char GetPromotionSymbol(
        MoveOptionId option)
    {
        if (option == PromotionOptions.Queen)
        {
            return 'Q';
        }

        if (option == PromotionOptions.Rook)
        {
            return 'R';
        }

        if (option == PromotionOptions.Bishop)
        {
            return 'B';
        }

        if (option == PromotionOptions.Knight)
        {
            return 'N';
        }

        throw new InvalidOperationException(
            "SAN has an unsupported promotion option.");
    }

    private static string GetSuffix(
        GameStatusId status)
    {
        if (status == StatusDefinitions.Checkmate)
        {
            return "#";
        }

        return status == StatusDefinitions.Check ? "+" : string.Empty;
    }

    private readonly record struct ParsedSan(
        MoveOptionId? CastlingOption,
        PieceDefinition? Definition,
        char? OriginFile,
        char? OriginRank,
        bool Captures,
        Square Destination,
        MoveOptionId? PromotionOption);
}
