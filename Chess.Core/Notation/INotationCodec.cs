// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Core.Notation;

public interface INotationCodec<T>
{
    T Parse(
        string notation);

    string Format(
        T value);
}
