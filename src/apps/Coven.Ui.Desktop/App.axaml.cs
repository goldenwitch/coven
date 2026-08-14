// SPDX-License-Identifier: BUSL-1.1

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Coven.Ui.Desktop.Theme;
using Coven.Ui.Desktop.ViewModels;
using Coven.Ui.Desktop.Views;

namespace Coven.Ui.Desktop;

internal sealed partial class App : Application
{
    private readonly SessionManager _manager;

    public App(SessionManager manager) => _manager = manager;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // The material textures are drawn, not loaded, so they have to exist in the resources
        // before anything resolves a brush from them.
        CovenTheme.Install(Resources);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Held by the closure rather than a field: Avalonia owns the Application's
            // lifetime, so the app cannot itself be disposable.
            MainWindowViewModel viewModel = new(_manager);
            MainWindow window = new() { DataContext = viewModel };

            // The dialog needs an owner, which only exists once the window does.
            viewModel.ShowOptionsDialog = options => new OptionsWindow(options).ShowDialog<bool>(window);

            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => viewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
