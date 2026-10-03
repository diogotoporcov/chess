// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Windows.Input;

namespace Chess.Desktop.Infrastructure.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception> _onError;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncRelayCommand(
        Func<Task> execute,
        Action<Exception> onError,
        Func<bool>? canExecute = null)
    {
        _execute = execute;
        _onError = onError;
        _canExecute = canExecute;
    }

    public bool CanExecute(
        object? parameter) =>
        !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(
        object? parameter)
    {
        try
        {
            await ExecuteAsync();
        }
        catch (Exception exception)
        {
            try
            {
                _onError(exception);
            }
            catch (Exception reportingException)
            {
                Trace.TraceError(
                    "Async command error reporting failed: {0}",
                    reportingException);
            }
        }
    }

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        _isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RefreshCanExecute() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
