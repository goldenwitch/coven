// SPDX-License-Identifier: BUSL-1.1

using Avalonia;
using Coven.Ui.Desktop.Settings;

namespace Coven.Ui.Desktop;

internal static class Program
{
    /// <summary>
    /// Loads settings, starts the Coven session on a background task, then runs the UI.
    /// </summary>
    /// <remarks>
    /// The ritual is the application's lifetime: it holds the daemon scope open, so it must
    /// outlive the window and be cancelled only once the UI has closed. The session manager
    /// owns the channel and journal hand-off, both of which survive a rebuild — the window
    /// still opens, and explains itself, when no session can be started.
    /// </remarks>
    [STAThread]
    public static int Main(string[] args)
    {
        SettingsStore store = new();
        AppSettings settings = store.Load();

        SessionManager manager = new(store, settings);
        manager.StartInitial();

        if (manager.StartupError is not null)
        {
            // Also to stderr: covenant validation messages name the exact route to add,
            // which is worth seeing when launching from a terminal.
            Console.Error.WriteLine($"Coven session not started: {manager.StartupError}");
        }

        try
        {
            return BuildAvaloniaApp(manager).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            manager.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static AppBuilder BuildAvaloniaApp(SessionManager manager)
        => AppBuilder.Configure(() => new App(manager))
            .UsePlatformDetect()
            .LogToTrace();
}
