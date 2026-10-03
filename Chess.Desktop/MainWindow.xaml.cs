// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;
using Chess.Desktop.ViewModels;

namespace Chess.Desktop;

public partial class MainWindow
{
    private bool _shutdownComplete;
    private bool _shutdownStarted;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnClosing(
        object? sender,
        CancelEventArgs e)
    {
        if (_shutdownComplete)
        {
            return;
        }

        e.Cancel = true;
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        IsEnabled = false;
        try
        {
            if (DataContext is ShellViewModel shell)
            {
                await shell.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError("Desktop shutdown failed: {0}", exception);
        }
        finally
        {
            _shutdownComplete = true;
            try
            {
                await Dispatcher.InvokeAsync(
                    () =>
                    {
                        try
                        {
                            Close();
                        }
                        catch (Exception exception)
                        {
                            Trace.TraceError(
                                "Desktop close failed: {0}",
                                exception);
                        }
                    },
                    DispatcherPriority.Background);
            }
            catch (Exception exception)
            {
                Trace.TraceError(
                    "Desktop dispatcher shutdown failed: {0}",
                    exception);
            }
        }
    }
}
