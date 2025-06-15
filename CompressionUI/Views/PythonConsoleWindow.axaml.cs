using Avalonia.Controls;
using Avalonia.Input;
using CompressionUI.ViewModels;
using System.Threading.Tasks;

namespace CompressionUI.Views;

public partial class PythonConsoleWindow : Window
{
    public PythonConsoleWindow()
    {
        InitializeComponent();
    }

    private async void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is PythonConsoleViewModel viewModel)
        {
            await viewModel.HandleKeyDown(e);
        }

        // Auto-scroll output to bottom after execution
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            await Task.Delay(100); // Small delay to let the output update
            var scrollViewer = this.FindControl<ScrollViewer>("OutputScrollViewer");
            scrollViewer?.ScrollToEnd();
        }
    }

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        
        // Focus the input box when window opens
        var inputBox = this.FindControl<TextBox>("InputTextBox");
        inputBox?.Focus();
    }
}