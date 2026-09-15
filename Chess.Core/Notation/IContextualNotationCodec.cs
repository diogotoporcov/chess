// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Core.Notation;

public interface IContextualNotationCodec<TContext, TValue>
{
    TValue Parse(
        TContext context,
        string notation);

    string Format(
        TContext context,
        TValue value);
}
