using Microsoft.Extensions.Logging;
using CompressionUI.Services;
using CompressionUI.Views;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CompressionUI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly PythonService _pythonService;
    private readonly INodeRegistry _nodeRegistry;
    private readonly NodeFactory _nodeFactory;
    private string _pythonStatus = "Python: Not Connected";
    private string _nodeRegistryStatus = "Nodes: Loading...";
    private PythonConsoleWindow? _consoleWindow;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger, 
        PythonService pythonService,
        INodeRegistry nodeRegistry,
        NodeFactory nodeFactory)
    {
        _logger = logger;
        _pythonService = pythonService;
        _nodeRegistry = nodeRegistry;
        _nodeFactory = nodeFactory;
        _logger.LogInformation("MainWindowViewModel initialized");
        
        // Commands
        OpenPythonConsoleCommand = new RelayCommand(() => OpenPythonConsole());
        TestNodeRegistryCommand = new RelayCommand(async () => await TestNodeRegistryAsync());
        
        // Initialize services in the background
        _ = InitializeServicesAsync();
    }

    public string Title { get; } = "CompressionUI - Neural Network Compression Platform";
    
    public string PythonStatus
    {
        get => _pythonStatus;
        set => SetField(ref _pythonStatus, value);
    }

    public string NodeRegistryStatus
    {
        get => _nodeRegistryStatus;
        set => SetField(ref _nodeRegistryStatus, value);
    }

    public ICommand OpenPythonConsoleCommand { get; }
    public ICommand TestNodeRegistryCommand { get; }

    private async Task InitializeServicesAsync()
    {
        // Initialize Python
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

        // Test Node Registry
        try
        {
            var nodeCount = _nodeRegistry.GetAllNodeTypes().Count();
            var categoryCount = _nodeRegistry.GetCategories().Count();
            
            NodeRegistryStatus = $"Nodes: {nodeCount} registered, {categoryCount} categories";
            _logger.LogInformation("Node registry loaded: {NodeCount} nodes in {CategoryCount} categories", 
                nodeCount, categoryCount);
        }
        catch (System.Exception ex)
        {
            NodeRegistryStatus = "Nodes: Error loading registry";
            _logger.LogError(ex, "Error accessing node registry");
        }
    }

    private async Task TestNodeRegistryAsync()
    {
        _logger.LogInformation("Testing node registry...");

        try
        {
            // List all registered nodes
            var allNodes = _nodeRegistry.GetAllNodeTypes().ToList();
            _logger.LogInformation("Found {Count} registered node types:", allNodes.Count);
            
            foreach (var nodeType in allNodes)
            {
                _logger.LogInformation("  - {DisplayName} ({Category}): {Description}", 
                    nodeType.DisplayName, nodeType.Category, nodeType.Description);
            }

            // Test creating nodes
            _logger.LogInformation("Testing node creation...");
            
            var debugNode = _nodeRegistry.CreateNode("DebugPrintNode");
            _logger.LogInformation("Created DebugPrintNode: {NodeId}", debugNode.Id);
            
            var variableNode = _nodeRegistry.CreateNode("VariableNode");
            _logger.LogInformation("Created VariableNode: {NodeId}", variableNode.Id);

            // Test a workflow template
            _logger.LogInformation("Testing workflow template...");
            var template = _nodeFactory.CreateWorkflowTemplate("simple-math");
            _logger.LogInformation("Created simple-math template with {Count} nodes", template.Count);

            _logger.LogInformation("Node registry test completed successfully!");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Node registry test failed");
        }
    }

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
}