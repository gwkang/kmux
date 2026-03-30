using System.Windows;
using System.Windows.Input;
using KMux.UI.Infrastructure;

namespace KMux.UI.Views;

public partial class KeybindingsHelpWindow : Window
{
    public KeybindingsHelpWindow()
    {
        InitializeComponent();
        ContentRendered += (_, _) => WindowAnimations.PlayEntry(RootContent, EntryScale);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
