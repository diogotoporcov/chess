// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Win32;

namespace Chess.Desktop.GameModes.Standard;

public interface IStockfishExecutablePicker
{
    string? Pick(
        string currentPath);
}

public sealed class
    WindowsStockfishExecutablePicker : IStockfishExecutablePicker
{
    public string? Pick(
        string currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            FileName = currentPath
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
