using Microsoft.Extensions.Logging;

namespace CompressionUI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    public MainWindowViewModel()
    {
        _logger = null!;
        _logger.LogInformation("MainWindowViewModel initialized with default constructor");
    }

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger)
    {
        _logger = logger;
        _logger.LogInformation("MainWindowViewModel initialized");
    }

    public static string Title => "CompressionUI - Neural Network Compression Platform";
}