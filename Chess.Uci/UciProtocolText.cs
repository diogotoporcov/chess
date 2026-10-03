// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Uci;

internal static class UciProtocolText
{
    public static void ValidateSingleLine(
        string value,
        string parameterName)
    {
        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException(
                "UCI protocol values must not contain CR, LF, or NUL.",
                parameterName);
        }
    }
}
