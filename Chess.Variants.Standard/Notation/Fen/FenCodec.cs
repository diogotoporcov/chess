// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using Chess.Core.Board;
using Chess.Core.Games.Variants;
using Chess.Core.Notation;
using Chess.Core.Pieces;
using Chess.Core.Sides;
using Chess.Variants.Standard.Board;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Notation.Fen;

public sealed class FenCodec : INotationCodec<StandardInitialState>
{
    private static readonly SquareCodec SquareCodec = new();

    public StandardInitialState Parse(
        string notation)
    {
        ArgumentNullException.ThrowIfNull(notation);

        var fields = notation.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);

        if (fields.Length != 6)
        {
            throw new FormatException("FEN must contain exactly six fields.");
        }

        var placements = ParsePiecePlacements(fields[0]);
        var sideToMove = ParseSideToMove(fields[1]);
        var castlingRights = ParseCastlingRights(fields[2]);
        var enPassantTarget = ParseEnPassantTarget(fields[3]);
        var halfmoveClock = ParseHalfmoveClock(fields[4]);
        var fullmoveNumber = ParseFullmoveNumber(fields[5]);

        try
        {
            return new StandardInitialState(
                placements,
                sideToMove,
                castlingRights,
                enPassantTarget,
                halfmoveClock,
                fullmoveNumber);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException(
                "FEN does not describe a valid Standard chess state.",
                exception);
        }
    }

    public string Format(
        StandardInitialState value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Format(
            FormatPiecePlacements(
                value.Placements.Select(placement => new SymbolPlacement(
                    placement.Square,
                    GetSymbol(placement.Side, placement.Definition)))),
            GetSideToken(value.SideToMove),
            value.CastlingRights,
            value.EnPassantTarget,
            value.HalfmoveClock,
            value.FullmoveNumber);
    }

    public string Format(
        StandardPositionFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return Format(
            FormatPiecePlacements(
                facts.PositionKey.PiecePlacements.Select(placement =>
                    new SymbolPlacement(
                        placement.Square,
                        GetSymbol(
                            placement.SideId,
                            placement.PieceDefinitionId)))),
            GetSideToken(facts.PositionKey.SideToMoveId),
            facts.CastlingRights,
            facts.EnPassantTarget,
            facts.HalfmoveClock,
            facts.FullmoveNumber);
    }

    private static List<InitialPiecePlacement> ParsePiecePlacements(
        string field)
    {
        var ranks = field.Split('/');

        if (ranks.Length != BoardLayout.Rows)
        {
            throw new FormatException(
                "FEN piece placement must contain exactly eight ranks.");
        }

        var placements = new List<InitialPiecePlacement>();

        for (var row = 0; row < ranks.Length; row++)
        {
            ParseRank(ranks[row], row, placements);
        }

        return placements;
    }

    private static void ParseRank(
        string rank,
        int row,
        List<InitialPiecePlacement> placements)
    {
        if (rank.Length == 0)
        {
            throw new FormatException(
                "FEN piece-placement ranks cannot be empty.");
        }

        var column = 0;
        var previousWasDigit = false;

        foreach (var symbol in rank)
        {
            if (symbol is >= '1' and <= '8')
            {
                if (previousWasDigit)
                {
                    throw new FormatException(
                        "FEN piece-placement ranks cannot contain " +
                        "consecutive empty-square digits.");
                }

                column += symbol - '0';
                previousWasDigit = true;
            }
            else
            {
                if (column >= BoardLayout.Columns)
                {
                    throw new FormatException(
                        "A FEN rank expands to more than eight squares.");
                }

                placements.Add(
                    ParsePiecePlacement(
                        symbol,
                        BoardLayout.GetSquare(row, column)));
                column++;
                previousWasDigit = false;
            }

            if (column > BoardLayout.Columns)
            {
                throw new FormatException(
                    "A FEN rank expands to more than eight squares.");
            }
        }

        if (column != BoardLayout.Columns)
        {
            throw new FormatException(
                "Each FEN rank must expand to exactly eight squares.");
        }
    }

    private static InitialPiecePlacement ParsePiecePlacement(
        char symbol,
        Square square)
    {
        return symbol switch
        {
            'P' => CreatePlacement(
                square,
                SideDefinitions.White,
                PieceDefinitions.Pawn),
            'N' => CreatePlacement(
                square,
                SideDefinitions.White,
                PieceDefinitions.Knight),
            'B' => CreatePlacement(
                square,
                SideDefinitions.White,
                PieceDefinitions.Bishop),
            'R' => CreatePlacement(
                square,
                SideDefinitions.White,
                PieceDefinitions.Rook),
            'Q' => CreatePlacement(
                square,
                SideDefinitions.White,
                PieceDefinitions.Queen),
            'K' => CreatePlacement(
                square,
                SideDefinitions.White,
                PieceDefinitions.King),
            'p' => CreatePlacement(
                square,
                SideDefinitions.Black,
                PieceDefinitions.Pawn),
            'n' => CreatePlacement(
                square,
                SideDefinitions.Black,
                PieceDefinitions.Knight),
            'b' => CreatePlacement(
                square,
                SideDefinitions.Black,
                PieceDefinitions.Bishop),
            'r' => CreatePlacement(
                square,
                SideDefinitions.Black,
                PieceDefinitions.Rook),
            'q' => CreatePlacement(
                square,
                SideDefinitions.Black,
                PieceDefinitions.Queen),
            'k' => CreatePlacement(
                square,
                SideDefinitions.Black,
                PieceDefinitions.King),
            _ => throw new FormatException(
                $"Unsupported FEN piece symbol '{symbol}'.")
        };
    }

    private static InitialPiecePlacement CreatePlacement(
        Square square,
        Side side,
        PieceDefinition definition)
    {
        return new InitialPiecePlacement(square, side, definition);
    }

    private static Side ParseSideToMove(
        string field)
    {
        return field switch
        {
            "w" => SideDefinitions.White,
            "b" => SideDefinitions.Black,
            _ => throw new FormatException(
                "FEN active color must be 'w' or 'b'.")
        };
    }

    private static CastlingRights ParseCastlingRights(
        string field)
    {
        if (field == "-")
        {
            return default;
        }

        var previousOrder = -1;
        var whiteKingSide = false;
        var whiteQueenSide = false;
        var blackKingSide = false;
        var blackQueenSide = false;

        foreach (var symbol in field)
        {
            var order = symbol switch
            {
                'K' => 0,
                'Q' => 1,
                'k' => 2,
                'q' => 3,
                _ => throw new FormatException(
                    $"Unsupported FEN castling symbol '{symbol}'.")
            };

            if (order <= previousOrder)
            {
                throw new FormatException(
                    "FEN castling rights must be unique and ordered KQkq.");
            }

            switch (symbol)
            {
                case 'K':
                    whiteKingSide = true;
                    break;
                case 'Q':
                    whiteQueenSide = true;
                    break;
                case 'k':
                    blackKingSide = true;
                    break;
                case 'q':
                    blackQueenSide = true;
                    break;
            }

            previousOrder = order;
        }

        return new CastlingRights(
            whiteKingSide,
            whiteQueenSide,
            blackKingSide,
            blackQueenSide);
    }

    private static Square? ParseEnPassantTarget(
        string field)
    {
        if (field == "-")
        {
            return null;
        }

        if (field.Length != 2 ||
            field[1] is not ('3' or '6'))
        {
            throw new FormatException(
                "FEN en passant target must be '-' or a square on rank " +
                "three or six.");
        }

        try
        {
            return SquareCodec.Parse(field);
        }
        catch (FormatException exception)
        {
            throw new FormatException(
                "FEN en passant target must be '-' or a square on rank " +
                "three or six.",
                exception);
        }
    }

    private static int ParseHalfmoveClock(
        string field)
    {
        return ParseDecimal(field, "halfmove clock", allowZero: true);
    }

    private static int ParseFullmoveNumber(
        string field)
    {
        return ParseDecimal(field, "fullmove number", allowZero: false);
    }

    private static int ParseDecimal(
        string field,
        string name,
        bool allowZero)
    {
        if (field.Length > 0 &&
            field.All(symbol => symbol is >= '0' and <= '9') &&
            int.TryParse(
                field,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value) &&
            (allowZero || value > 0))
        {
            return value;
        }

        var range = allowZero ? "non-negative" : "positive";

        throw new FormatException(
            $"FEN {name} must be a {range} decimal integer.");
    }

    private static string Format(
        string piecePlacements,
        string sideToMove,
        CastlingRights castlingRights,
        Square? enPassantTarget,
        int halfmoveClock,
        int fullmoveNumber)
    {
        return string.Join(
            ' ',
            piecePlacements,
            sideToMove,
            FormatCastlingRights(castlingRights),
            FormatEnPassantTarget(enPassantTarget),
            halfmoveClock.ToString(CultureInfo.InvariantCulture),
            fullmoveNumber.ToString(CultureInfo.InvariantCulture));
    }

    private static string FormatPiecePlacements(
        IEnumerable<SymbolPlacement> placements)
    {
        var symbolsBySquare = new Dictionary<Square, char>();

        foreach (var placement in placements)
        {
            EnsureStandardSquare(placement.Square);

            if (!symbolsBySquare.TryAdd(placement.Square, placement.Symbol))
            {
                throw new InvalidOperationException(
                    "Standard FEN cannot contain multiple pieces on one " +
                    "square.");
            }
        }

        var builder = new StringBuilder();

        for (var row = 0; row < BoardLayout.Rows; row++)
        {
            if (row > 0)
            {
                builder.Append('/');
            }

            var emptySquares = 0;

            for (var column = 0; column < BoardLayout.Columns; column++)
            {
                var square = BoardLayout.GetSquare(row, column);

                if (!symbolsBySquare.TryGetValue(square, out var symbol))
                {
                    emptySquares++;
                    continue;
                }

                if (emptySquares > 0)
                {
                    builder.Append((char)('0' + emptySquares));
                    emptySquares = 0;
                }

                builder.Append(symbol);
            }

            if (emptySquares > 0)
            {
                builder.Append((char)('0' + emptySquares));
            }
        }

        return builder.ToString();
    }

    private static string GetSideToken(
        Side side)
    {
        if (ReferenceEquals(side, SideDefinitions.White))
        {
            return "w";
        }

        if (ReferenceEquals(side, SideDefinitions.Black))
        {
            return "b";
        }

        throw new InvalidOperationException(
            "Standard FEN cannot format an unsupported side.");
    }

    private static string GetSideToken(
        string sideId)
    {
        if (StringComparer.Ordinal.Equals(sideId, SideDefinitions.White.Id))
        {
            return "w";
        }

        if (StringComparer.Ordinal.Equals(sideId, SideDefinitions.Black.Id))
        {
            return "b";
        }

        throw new InvalidOperationException(
            "Standard position facts contain an unsupported side-to-move id.");
    }

    private static char GetSymbol(
        Side side,
        PieceDefinition definition)
    {
        var isWhite = ReferenceEquals(side, SideDefinitions.White);

        if (!isWhite &&
            !ReferenceEquals(side, SideDefinitions.Black))
        {
            throw new InvalidOperationException(
                "Standard FEN cannot format an unsupported piece side.");
        }

        var symbol = GetPieceSymbol(definition);

        return isWhite ? char.ToUpperInvariant(symbol) : symbol;
    }

    private static char GetSymbol(
        string sideId,
        string pieceDefinitionId)
    {
        var isWhite = StringComparer.Ordinal.Equals(
            sideId,
            SideDefinitions.White.Id);

        if (!isWhite &&
            !StringComparer.Ordinal.Equals(sideId, SideDefinitions.Black.Id))
        {
            throw new InvalidOperationException(
                "Standard position facts contain an unsupported piece side " +
                "id.");
        }

        var symbol = GetPieceSymbol(pieceDefinitionId);

        return isWhite ? char.ToUpperInvariant(symbol) : symbol;
    }

    private static char GetPieceSymbol(
        PieceDefinition definition)
    {
        if (ReferenceEquals(definition, PieceDefinitions.Pawn))
        {
            return 'p';
        }

        if (ReferenceEquals(definition, PieceDefinitions.Knight))
        {
            return 'n';
        }

        if (ReferenceEquals(definition, PieceDefinitions.Bishop))
        {
            return 'b';
        }

        if (ReferenceEquals(definition, PieceDefinitions.Rook))
        {
            return 'r';
        }

        if (ReferenceEquals(definition, PieceDefinitions.Queen))
        {
            return 'q';
        }

        if (ReferenceEquals(definition, PieceDefinitions.King))
        {
            return 'k';
        }

        throw new InvalidOperationException(
            "Standard FEN cannot format an unsupported piece definition.");
    }

    private static char GetPieceSymbol(
        string pieceDefinitionId)
    {
        if (StringComparer.Ordinal.Equals(
                pieceDefinitionId,
                PieceDefinitions.Pawn.Id.Value))
        {
            return 'p';
        }

        if (StringComparer.Ordinal.Equals(
                pieceDefinitionId,
                PieceDefinitions.Knight.Id.Value))
        {
            return 'n';
        }

        if (StringComparer.Ordinal.Equals(
                pieceDefinitionId,
                PieceDefinitions.Bishop.Id.Value))
        {
            return 'b';
        }

        if (StringComparer.Ordinal.Equals(
                pieceDefinitionId,
                PieceDefinitions.Rook.Id.Value))
        {
            return 'r';
        }

        if (StringComparer.Ordinal.Equals(
                pieceDefinitionId,
                PieceDefinitions.Queen.Id.Value))
        {
            return 'q';
        }

        if (StringComparer.Ordinal.Equals(
                pieceDefinitionId,
                PieceDefinitions.King.Id.Value))
        {
            return 'k';
        }

        throw new InvalidOperationException(
            "Standard position facts contain an unsupported piece " +
            "definition id.");
    }

    private static string FormatCastlingRights(
        CastlingRights rights)
    {
        var builder = new StringBuilder();

        if (rights.WhiteKingSide)
        {
            builder.Append('K');
        }

        if (rights.WhiteQueenSide)
        {
            builder.Append('Q');
        }

        if (rights.BlackKingSide)
        {
            builder.Append('k');
        }

        if (rights.BlackQueenSide)
        {
            builder.Append('q');
        }

        return builder.Length == 0 ? "-" : builder.ToString();
    }

    private static string FormatEnPassantTarget(
        Square? target)
    {
        if (target is not { } square)
        {
            return "-";
        }

        EnsureStandardSquare(square);

        var row = BoardLayout.GetRow(square);

        if (row is not (2 or 5))
        {
            throw new InvalidOperationException(
                "Standard FEN en passant targets must be on rank three or " +
                "six.");
        }

        return SquareCodec.Format(square);
    }

    private static void EnsureStandardSquare(
        Square square)
    {
        try
        {
            BoardLayout.GetRow(square);
            BoardLayout.GetColumn(square);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException(
                "Standard FEN cannot format a square outside the Standard " +
                "board.",
                exception);
        }
    }

    private readonly record struct SymbolPlacement(Square Square, char Symbol);
}
