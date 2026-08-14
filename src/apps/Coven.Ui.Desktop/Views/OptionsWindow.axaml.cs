// SPDX-License-Identifier: BUSL-1.1

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Coven.Ui.Desktop.ViewModels;

namespace Coven.Ui.Desktop.Views;

internal sealed partial class OptionsWindow : Window
{
    public OptionsWindow() => InitializeComponent();

    public OptionsWindow(OptionsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += saved => Close(saved);

        // Dialogs need an owner, which only exists here.
        viewModel.ShowModelBrowser = browser => new ModelBrowserWindow(browser).ShowDialog<string?>(this);
        viewModel.PickFolder = PickFolderAsync;
    }

    private async Task<string?> PickFolderAsync(string startingPath)
    {
        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(startingPath))
        {
            try
            {
                start = await StorageProvider.TryGetFolderFromPathAsync(startingPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // A path that no longer exists just means no starting location.
            }
        }

        IReadOnlyList<IStorageFolder> picked = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Choose a folder for local models",
                AllowMultiple = false,
                SuggestedStartLocation = start
            });

        return picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
