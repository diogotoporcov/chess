// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Desktop.GameModes;
using Chess.Desktop.GameModes.Standard;
using Chess.Variants.Standard;

namespace Chess.Desktop.ViewModels;

public sealed class ShellViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly GameModeCatalog _catalog;
    private readonly IStandardEngineSessionFactory _sessionFactory;
    private readonly IStockfishExecutablePicker _picker;
    private ViewModelBase _currentScreen = null!;
    private StandardGameSetupViewModel? _standardSetup;
    private bool _disposed;
    private Task? _disposeTask;

    public ViewModelBase CurrentScreen
    {
        get => _currentScreen;
        private set => SetProperty(ref _currentScreen, value);
    }

    public ShellViewModel(
        GameModeCatalog catalog,
        IStandardEngineSessionFactory sessionFactory,
        IStockfishExecutablePicker picker)
    {
        _catalog = catalog;
        _sessionFactory = sessionFactory;
        _picker = picker;
        ShowModeSelection();
    }

    private void ShowModeSelection()
    {
        if (_disposed)
        {
            return;
        }

        CurrentScreen = new ModeSelectionViewModel(_catalog, SelectMode);
    }

    private void SelectMode(
        GameModeDefinition mode)
    {
        if (_disposed)
        {
            return;
        }

        if (mode.Variant.Id != Variant.Definition.Id)
        {
            throw new NotSupportedException(
                "Only Standard setup is available.");
        }

        _standardSetup ??= new StandardGameSetupViewModel(
            _sessionFactory,
            _picker,
            (configuration, session) =>
                StartGameAsync(mode, configuration, session),
            ShowModeSelection);
        CurrentScreen = _standardSetup;
    }

    private Task StartGameAsync(
        GameModeDefinition mode,
        StandardSessionConfiguration configuration,
        IStandardEngineSession session)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ShellViewModel));
        }

        CurrentScreen = new GameViewModel(
            mode,
            configuration,
            session,
            BackFromGameAsync);
        return Task.CompletedTask;
    }

    private async Task BackFromGameAsync()
    {
        if (CurrentScreen is GameViewModel game)
        {
            await game.DisposeAsync();
            if (!_disposed &&
                _standardSetup is not null)
            {
                CurrentScreen = _standardSetup;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _disposeTask ??= DisposeCoreAsync();
        return new ValueTask(_disposeTask);
    }

    private async Task DisposeCoreAsync()
    {
        _disposed = true;
        var current = CurrentScreen;
        if (current is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }

        if (_standardSetup is not null &&
            !ReferenceEquals(current, _standardSetup))
        {
            await _standardSetup.DisposeAsync();
        }
    }
}
