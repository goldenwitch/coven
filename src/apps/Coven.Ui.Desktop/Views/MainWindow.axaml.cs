// SPDX-License-Identifier: BUSL-1.1

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Coven.Ui.Desktop.Views;

internal sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
