using Microsoft.Extensions.Logging;
using CompressionUI.Services;
using CompressionUI.Views;
using System.Windows.Input;

namespace CompressionUI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly PythonService _pythonService;
    private string _pythonStatus = "Python: Not Connected";
    private PythonConsoleWindow? _consoleWindow;

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, PythonService pythonService)
    {
        _logger = logger;
        _pythonService = pythonService;
        _logger.LogInformation("MainWindowViewModel initialized");
        
        // Commands
        OpenPythonConsoleCommand = new RelayCommand(() => OpenPythonConsole());
        
        // Initialize Python in the background
        _ = InitializePythonAsync();
    }

    public string Title { get; } = "CompressionUI - Neural Network Compression Platform";
    
    public string PythonStatus
    {
        get => _pythonStatus;
        set => SetField(ref _pythonStatus, value);
    }

    public ICommand OpenPythonConsoleCommand { get; }

    private void OpenPythonConsole()
    {
        if (_consoleWindow == null || _consoleWindow.IsVisible == false)
        {
            var consoleViewModel = new PythonConsoleViewModel(_pythonService, 
                LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<PythonConsoleViewModel>());
            
            _consoleWindow = new PythonConsoleWindow
            {
                DataContext = consoleViewModel
            };
            
            _consoleWindow.Closed += (s, e) => _consoleWindow = null;
        }
        
        _consoleWindow.Show();
        _consoleWindow.Activate();
    }

    private async Task InitializePythonAsync()
    {
        try
        {
            PythonStatus = "Python: Initializing...";
            
            var success = await _pythonService.InitializePythonAsync();
            if (success)
            {
                PythonStatus = "Python: Connected";
                _logger.LogInformation("Python service initialized successfully");
                
                // Run environment test
                var testResult = await _pythonService.TestPythonEnvironmentAsync();
                if (testResult)
                {
                    PythonStatus = "Python: Ready (All tests passed)";
                }
                else
                {
                    PythonStatus = "Python: Connected (Some tests failed)";
                }
            }
            else
            {
                PythonStatus = "Python: Failed to initialize";
                _logger.LogError("Failed to initialize Python service");
            }
        }
        catch (System.Exception ex)
        {
            PythonStatus = "Python: Error";
            _logger.LogError(ex, "Error initializing Python service");
        }
    }
}