// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using Chess.Core.Movement;
using Chess.Core.Notation;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Notation.Fen;
using Chess.Variants.Standard.Notation.San;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Notation.Pgn;

public sealed class PgnCodec : INotationCodec<PgnGame>
{
    private static readonly string[] SevenTagRoster =
    [
        "Event", "Site", "Date", "Round", "White", "Black", "Result"
    ];

    private static readonly FenCodec FenCodec = new();
    private static readonly SanCodec SanCodec = new();

    public PgnGame Parse(
        string notation)
    {
        ArgumentNullException.ThrowIfNull(notation);

        var scanner = new Scanner(notation);
        var tags = ParseTags(scanner);
        var initialState = ParseInitialState(tags);
        var result = ParseResultTag(tags["Result"]);
        var mainline = ParseMovetext(scanner, initialState, result);

        return new PgnGame(
            tags["Event"],
            tags["Site"],
            tags["Date"],
            tags["Round"],
            tags["White"],
            tags["Black"],
            result,
            initialState,
            mainline,
            tags.Where(tag =>
                !SevenTagRoster.Contains(tag.Key, StringComparer.Ordinal) &&
                tag.Key is not "SetUp" and not "FEN"));
    }

    public string Format(
        PgnGame value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder();
        AppendTag(builder, "Event", value.Event);
        AppendTag(builder, "Site", value.Site);
        AppendTag(builder, "Date", value.Date);
        AppendTag(builder, "Round", value.Round);
        AppendTag(builder, "White", value.White);
        AppendTag(builder, "Black", value.Black);
        AppendTag(builder, "Result", FormatResult(value.Result));

        var tags = new Dictionary<string, string>(
            value.AdditionalTags,
            StringComparer.Ordinal);

        if (!StringComparer.Ordinal.Equals(
                FenCodec.Format(value.InitialState),
                FenCodec.Format(StandardInitialState.Default)))
        {
            tags.Add("SetUp", "1");
            tags.Add("FEN", FenCodec.Format(value.InitialState));
        }

        foreach (var tag in tags.OrderBy(
                     tag => tag.Key,
                     StringComparer.Ordinal))
        {
            AppendTag(builder, tag.Key, tag.Value);
        }

        builder.Append('\n');
        AppendMovetext(builder, value);
        builder.Append("\n\n");
        return builder.ToString();
    }

    private static Dictionary<string, string> ParseTags(
        Scanner scanner)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        scanner.SkipWhitespace();

        while (scanner.Peek() == '[')
        {
            scanner.Read();
            scanner.SkipWhitespace();
            var name = scanner.ReadTagName();
            scanner.SkipWhitespace();
            var value = scanner.ReadQuotedValue();
            scanner.SkipWhitespace();
            scanner.Require(']');

            if (!tags.TryAdd(name, value))
            {
                throw new FormatException("PGN tag names must be unique.");
            }

            scanner.SkipWhitespace();
        }

        foreach (var tag in SevenTagRoster)
        {
            if (!tags.ContainsKey(tag))
            {
                throw new FormatException($"PGN requires the '{tag}' tag.");
            }
        }

        return tags;
    }

    private static StandardInitialState ParseInitialState(
        IReadOnlyDictionary<string, string> tags)
    {
        var hasSetup = tags.TryGetValue("SetUp", out var setup);
        var hasFen = tags.TryGetValue("FEN", out var fen);

        if (!hasSetup &&
            !hasFen)
        {
            return StandardInitialState.Default;
        }

        if (!hasSetup ||
            setup is not ("0" or "1"))
        {
            throw new FormatException("PGN SetUp must be '0' or '1'.");
        }

        if (setup == "0")
        {
            if (hasFen)
            {
                throw new FormatException("PGN SetUp '0' cannot include FEN.");
            }

            return StandardInitialState.Default;
        }

        if (!hasFen)
        {
            throw new FormatException("PGN SetUp '1' requires FEN.");
        }

        try
        {
            return FenCodec.Parse(fen!);
        }
        catch (FormatException exception)
        {
            throw new FormatException("PGN FEN is invalid.", exception);
        }
    }

    private static List<Move> ParseMovetext(
        Scanner scanner,
        StandardInitialState initialState,
        PgnResult expectedResult)
    {
        var game = Variant.CreateGame(initialState);
        var moves = new List<Move>();
        var foundResult = false;

        while (true)
        {
            scanner.SkipWhitespace();

            if (scanner.IsEnd)
            {
                break;
            }

            if (scanner.TryReadMoveNumber(out var number))
            {
                ValidateMoveNumber(initialState, moves.Count, number);
                continue;
            }

            scanner.RejectUnsupportedMovetextConstruct();
            var token = scanner.ReadMovetextToken();

            if (TryParseResult(token, out var result))
            {
                if (result != expectedResult || foundResult)
                {
                    throw new FormatException("PGN result markers must agree.");
                }

                foundResult = true;
                scanner.SkipWhitespace();

                if (!scanner.IsEnd)
                {
                    throw new FormatException(
                        "PGN content cannot follow the termination marker.");
                }

                break;
            }

            try
            {
                var move = SanCodec.Parse(game, token);
                game.Execute(move);
                moves.Add(move);
            }
            catch (FormatException exception)
            {
                throw new FormatException(
                    "PGN contains invalid SAN.",
                    exception);
            }
        }

        if (!foundResult)
        {
            throw new FormatException(
                "PGN movetext requires a termination marker.");
        }

        return moves;
    }

    private static void ValidateMoveNumber(
        StandardInitialState initialState,
        int moveCount,
        int number)
    {
        var expected = initialState.FullmoveNumber +
                       (initialState.SideToMove == SideDefinitions.Black
                           ? moveCount + 1
                           : moveCount) /
                       2;

        if (number != expected)
        {
            throw new FormatException(
                "PGN move number does not match the current position.");
        }
    }

    private static void AppendMovetext(
        StringBuilder builder,
        PgnGame value)
    {
        var game = Variant.CreateGame(value.InitialState);
        var tokens = new List<string>();
        var fullmove = value.InitialState.FullmoveNumber;
        var isBlack = ReferenceEquals(
            value.InitialState.SideToMove,
            SideDefinitions.Black);

        foreach (var move in value.Mainline)
        {
            if (!isBlack)
            {
                tokens.Add(
                    fullmove.ToString(CultureInfo.InvariantCulture) + ".");
            }
            else if (tokens.Count == 0)
            {
                tokens.Add(
                    fullmove.ToString(CultureInfo.InvariantCulture) + "...");
            }

            tokens.Add(SanCodec.Format(game, move));
            game.Execute(move);

            if (isBlack)
            {
                fullmove++;
            }

            isBlack = !isBlack;
        }

        tokens.Add(FormatResult(value.Result));
        var lineLength = 0;

        foreach (var token in tokens)
        {
            if (lineLength == 0)
            {
                builder.Append(token);
                lineLength = token.Length;
            }
            else if (lineLength + 1 + token.Length <= 79)
            {
                builder
                    .Append(' ')
                    .Append(token);
                lineLength += token.Length + 1;
            }
            else
            {
                builder
                    .Append('\n')
                    .Append(token);
                lineLength = token.Length;
            }
        }
    }

    private static void AppendTag(
        StringBuilder builder,
        string name,
        string value)
    {
        builder
            .Append('[')
            .Append(name)
            .Append(" \"");

        foreach (var character in value)
        {
            if (character is '"' or '\\')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        builder.Append("\"]\n");
    }

    private static PgnResult ParseResultTag(
        string value)
    {
        return TryParseResult(value, out var result)
            ? result
            : throw new FormatException("PGN Result has an invalid value.");
    }

    private static bool TryParseResult(
        string token,
        out PgnResult result)
    {
        result = token switch
        {
            "1-0" => PgnResult.WhiteWin,
            "0-1" => PgnResult.BlackWin,
            "1/2-1/2" => PgnResult.Draw,
            "*" => PgnResult.Unknown,
            _ => default
        };

        return token is "1-0" or "0-1" or "1/2-1/2" or "*";
    }

    private static string FormatResult(
        PgnResult result)
    {
        return result switch
        {
            PgnResult.WhiteWin => "1-0",
            PgnResult.BlackWin => "0-1",
            PgnResult.Draw => "1/2-1/2",
            PgnResult.Unknown => "*",
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private sealed class Scanner
    {
        private readonly string _text;
        private int _position;

        public bool IsEnd => _position == _text.Length;

        public Scanner(
            string text)
        {
            _text = text.Length > 0 && text[0] == '\ufeff' ? text[1..] : text;
        }

        public char Peek() => IsEnd ? '\0' : _text[_position];

        public char Read() =>
            IsEnd
                ? throw new FormatException("PGN ended unexpectedly.")
                : _text[_position++];

        public void Require(
            char expected)
        {
            if (Read() != expected)
            {
                throw new FormatException("PGN has malformed syntax.");
            }
        }

        public void SkipWhitespace()
        {
            while (!IsEnd &&
                   Peek() is ' ' or '\t' or '\r' or '\n')
            {
                _position++;
            }
        }

        public string ReadTagName()
        {
            var start = _position;

            while (!IsEnd &&
                   Peek() is not (' ' or '\t' or '\r' or '\n' or ']' or '"'))
            {
                _position++;
            }

            var name = _text[start.._position];

            if (!PgnGame.IsValidTagName(name))
            {
                throw new FormatException("PGN tag name is invalid.");
            }

            return name;
        }

        public bool TryReadMoveNumber(
            out int number)
        {
            number = 0;

            if (IsEnd || !char.IsAsciiDigit(Peek()))
            {
                return false;
            }

            var start = _position;

            while (!IsEnd &&
                   char.IsAsciiDigit(Peek()))
            {
                _position++;
            }

            if (!IsEnd &&
                Peek() is not (' ' or '\t' or '\r' or '\n' or '.'))
            {
                _position = start;
                return false;
            }

            if (!int.TryParse(
                    _text[start.._position],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out number))
            {
                throw new FormatException("PGN move number is invalid.");
            }

            SkipWhitespace();

            while (!IsEnd &&
                   Peek() == '.')
            {
                _position++;
            }

            return true;
        }

        public string ReadQuotedValue()
        {
            Require('"');
            var value = new StringBuilder();

            while (true)
            {
                var character = Read();

                if (character == '"')
                {
                    return value.ToString();
                }

                if (char.IsControl(character))
                {
                    throw new FormatException(
                        "PGN tag values cannot contain controls.");
                }

                if (character != '\\')
                {
                    value.Append(character);
                    continue;
                }

                var escaped = Read();

                if (escaped is not ('"' or '\\'))
                {
                    throw new FormatException(
                        "PGN tag string has an invalid escape.");
                }

                value.Append(escaped);
            }
        }

        public void RejectUnsupportedMovetextConstruct()
        {
            if (Peek() is '{' or ';' or '(' or ')' or '$' or '[' or ']')
            {
                throw new FormatException(
                    "PGN contains an unsupported construct.");
            }

            if (Peek() == '%' &&
                (_position == 0 || _text[_position - 1] is '\n' or '\r'))
            {
                throw new FormatException(
                    "PGN escape commands are unsupported.");
            }
        }

        public string ReadMovetextToken()
        {
            var start = _position;

            while (!IsEnd &&
                   Peek() is not (' ' or '\t' or '\r' or '\n' or '{' or ';'
                       or '(' or ')' or '$' or '[' or ']'))
            {
                _position++;
            }

            if (start == _position)
            {
                throw new FormatException("PGN has malformed movetext.");
            }

            return _text[start.._position];
        }
    }
}
