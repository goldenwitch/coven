// SPDX-License-Identifier: BUSL-1.1

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Coven.Ui.Desktop.ViewModels;

namespace Coven.Ui.Desktop.Views;

internal sealed partial class ModelBrowserWindow : Window
{
    public ModelBrowserWindow() => InitializeComponent();

    public ModelBrowserWindow(ModelBrowserViewModel viewModel)
        : this()
    {
        DataContext = viewModel;

        // Carries the downloaded path back so Options can select it immediately.
        viewModel.CloseRequested += path => Close(path);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
