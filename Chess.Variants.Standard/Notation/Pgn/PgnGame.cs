// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using Chess.Core.Movement;
using Chess.Variants.Standard.Games;

namespace Chess.Variants.Standard.Notation.Pgn;

public sealed class PgnGame
{
    private static readonly HashSet<string> ReservedTagNames =
    [
        "Event", "Site", "Date", "Round", "White", "Black", "Result",
        "SetUp", "FEN"
    ];

    private readonly ReadOnlyCollection<Move> _mainline;
    private readonly ReadOnlyDictionary<string, string> _additionalTags;

    public string Event { get; }

    public string Site { get; }

    public string Date { get; }

    public string Round { get; }

    public string White { get; }

    public string Black { get; }

    public PgnResult Result { get; }

    public StandardInitialState InitialState { get; }
    public IReadOnlyList<Move> Mainline => _mainline;

    public IReadOnlyDictionary<string, string> AdditionalTags =>
        _additionalTags;

    public PgnGame(
        string @event,
        string site,
        string date,
        string round,
        string white,
        string black,
        PgnResult result,
        StandardInitialState initialState,
        IEnumerable<Move> mainline,
        IEnumerable<KeyValuePair<string, string>> additionalTags)
    {
        ValidateValue(@event, nameof(@event));
        ValidateValue(site, nameof(site));
        ValidateValue(date, nameof(date));
        ValidateValue(round, nameof(round));
        ValidateValue(white, nameof(white));
        ValidateValue(black, nameof(black));

        if (!Enum.IsDefined(result))
        {
            throw new ArgumentOutOfRangeException(nameof(result));
        }

        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(mainline);
        ArgumentNullException.ThrowIfNull(additionalTags);

        var copiedTags = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var tag in additionalTags)
        {
            ValidateTagName(tag.Key, nameof(additionalTags));
            ValidateValue(tag.Value, nameof(additionalTags));

            if (ReservedTagNames.Contains(tag.Key))
            {
                throw new ArgumentException(
                    "Supplemental tags cannot redefine a reserved tag.",
                    nameof(additionalTags));
            }

            if (!copiedTags.TryAdd(tag.Key, tag.Value))
            {
                throw new ArgumentException(
                    "Supplemental tag names must be unique.",
                    nameof(additionalTags));
            }
        }

        Event = @event;
        Site = site;
        Date = date;
        Round = round;
        White = white;
        Black = black;
        Result = result;
        InitialState = initialState;
        _mainline = Array.AsReadOnly(mainline.ToArray());
        _additionalTags = new ReadOnlyDictionary<string, string>(copiedTags);
    }

    internal static bool IsValidTagName(
        string name)
    {
        return name.Length > 0 &&
               name.All(character => character is >= 'A' and <= 'Z'
                   or >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }

    private static void ValidateTagName(
        string name,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!IsValidTagName(name))
        {
            throw new ArgumentException(
                "PGN tag names may contain only letters, digits, and underscores.",
                parameterName);
        }
    }

    private static void ValidateValue(
        string value,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Any(character => char.IsControl(character)))
        {
            throw new ArgumentException(
                "PGN tag values cannot contain control characters.",
                parameterName);
        }
    }
}
