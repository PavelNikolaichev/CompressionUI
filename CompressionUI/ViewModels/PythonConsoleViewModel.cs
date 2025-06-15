using CompressionUI.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Input;

namespace CompressionUI.ViewModels;

public class PythonConsoleViewModel : ViewModelBase
{
    private readonly PythonService _pythonService;
    private readonly ILogger<PythonConsoleViewModel> _logger;
    
    private string _inputText = "";
    private string _outputText = "";
    private bool _isExecuting = false;
    private int _historyIndex = -1;
    
    public ObservableCollection<string> CommandHistory { get; } = new();
    
    public PythonConsoleViewModel(PythonService pythonService, ILogger<PythonConsoleViewModel> logger)
    {
        _pythonService = pythonService;
        _logger = logger;
        
        // Subscribe to Python service events
        _pythonService.OutputReceived += OnPythonOutput;
        _pythonService.ErrorReceived += OnPythonError;
        
        // Commands
        ExecuteCommand = new RelayCommand(async () => await ExecuteCodeAsync(), () => !IsExecuting);
        ClearCommand = new RelayCommand(() => OutputText = "");
        
        // Initialize with welcome message
        OutputText = "Python Console Ready\n" +
                    "Type Python code and press Ctrl+Enter to execute\n" +
                    "Use Up/Down arrows for command history\n" +
                    "================================\n\n";
    }

    public string InputText
    {
        get => _inputText;
        set => SetField(ref _inputText, value);
    }

    public string OutputText
    {
        get => _outputText;
        set => SetField(ref _outputText, value);
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        set => SetField(ref _isExecuting, value);
    }

    public ICommand ExecuteCommand { get; }
    public ICommand ClearCommand { get; }

    private async Task ExecuteCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsExecuting)
            return;

        var code = InputText.Trim();
        
        // Add to history
        if (!string.IsNullOrEmpty(code) && (CommandHistory.Count == 0 || CommandHistory.Last() != code))
        {
            CommandHistory.Add(code);
        }
        _historyIndex = CommandHistory.Count;

        // Show command in output
        OutputText += $">>> {code}\n";
        
        // Clear input and set executing state
        InputText = "";
        IsExecuting = true;

        try
        {
            var result = await _pythonService.ExecutePythonCodeAsync(code);
            
            if (!string.IsNullOrEmpty(result.Output))
            {
                OutputText += result.Output;
                if (!result.Output.EndsWith("\n"))
                    OutputText += "\n";
            }
            
            if (!string.IsNullOrEmpty(result.Error))
            {
                OutputText += $"ERROR: {result.Error}\n";
            }
            
            if (!result.Success && string.IsNullOrEmpty(result.Error))
            {
                OutputText += "Execution failed\n";
            }
        }
        catch (Exception ex)
        {
            OutputText += $"ERROR: {ex.Message}\n";
            _logger.LogError(ex, "Error executing Python code");
        }
        finally
        {
            IsExecuting = false;
            OutputText += "\n";
        }
    }

    public async Task HandleKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            // TODO: input field doesn't work with Shift+Enter due to the way Avalonia handles key events
            case Key.Enter when !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                await ExecuteCodeAsync();
                e.Handled = true;
                break;
                
            case Key.Up:
                NavigateHistory(-1);
                e.Handled = true;
                break;
                
            case Key.Down:
                NavigateHistory(1);
                e.Handled = true;
                break;
        }
    }

    private void NavigateHistory(int direction)
    {
        if (CommandHistory.Count == 0) return;

        _historyIndex += direction;
        
        if (_historyIndex < 0)
            _historyIndex = 0;
        else if (_historyIndex >= CommandHistory.Count)
        {
            _historyIndex = CommandHistory.Count;
            InputText = "";
            return;
        }

        InputText = CommandHistory[_historyIndex];
    }

    private void OnPythonOutput(object? sender, string output)
    {
        // This will be called from Python service for real-time output
        // For now, output is handled in ExecuteCodeAsync
    }

    private void OnPythonError(object? sender, string error)
    {
        // This will be called from Python service for real-time errors
        // For now, errors are handled in ExecuteCodeAsync
    }
}

// Simple RelayCommand implementation
public class RelayCommand : ICommand
{
    private readonly Func<Task>? _executeAsync;
    private readonly Action? _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        if (_executeAsync != null)
            await _executeAsync();
        else
            _execute?.Invoke();
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}