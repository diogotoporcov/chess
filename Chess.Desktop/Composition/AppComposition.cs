// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Desktop.GameModes;
using Chess.Desktop.GameModes.Standard;
using Chess.Desktop.ViewModels;

namespace Chess.Desktop.Composition;

public static class AppComposition
{
    public static ShellViewModel CreateShell()
    {
        IGameModeProvider[] providers = [new StandardGameModeProvider()];

        var catalog = new GameModeCatalog(providers);

        return new ShellViewModel(
            catalog,
            new StandardEngineSessionFactory(),
            new WindowsStockfishExecutablePicker());
    }
}
