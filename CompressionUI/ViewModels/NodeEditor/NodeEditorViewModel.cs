using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using CompressionUI.Models.Nodes;
using CompressionUI.Services;
using CompressionUI.ViewModels.Nodes;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace CompressionUI.ViewModels.NodeEditor;

public class NodeEditorViewModel : ReactiveObject
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly NodeFactory _nodeFactory;
    private readonly NodeExecutionService _executionService;
    private readonly NodeSerializationService _serializationService;
    private readonly ILogger<NodeEditorViewModel> _logger;

    private bool _isExecuting;
    private string _statusText = "Ready - Create some nodes!";

    public NodeEditorViewModel(
        INodeRegistry nodeRegistry,
        NodeFactory nodeFactory,
        NodeExecutionService executionService,
        NodeSerializationService serializationService,
        ILogger<NodeEditorViewModel> logger)
    {
        _nodeRegistry = nodeRegistry;
        _nodeFactory = nodeFactory;
        _executionService = executionService;
        _serializationService = serializationService;
        _logger = logger;

        // Initialize collections
        Operations = new ObservableCollection<VisualNodeViewModel>();
        Connections = new ObservableCollection<VisualConnectionViewModel>();
        SelectedOperations = new ObservableCollection<VisualNodeViewModel>();
        AvailableOperations = new ObservableCollection<NodeTypeInfo>();

        // Initialize commands
        ExecuteGraphCommand = new RelayCommand(async () => await ExecuteGraphAsync(), () => !IsExecuting && Operations.Any());
        ClearGraphCommand = new RelayCommand(ClearGraph, () => Operations.Any());
        CreateNodeCommand = new RelayCommand<string>(CreateNode);
        DeleteSelectionCommand = new RelayCommand(DeleteSelection, () => SelectedOperations.Any());

        // Load available operations
        LoadAvailableOperations();

        // React to collection changes
        Operations.CollectionChanged += (_, _) => UpdateCanExecuteCommands();
        SelectedOperations.CollectionChanged += (_, _) => UpdateCanExecuteCommands();
    }

    // Properties for NodifyAvalonia binding
    public ObservableCollection<VisualNodeViewModel> Operations { get; }
    public ObservableCollection<VisualConnectionViewModel> Connections { get; }
    public ObservableCollection<VisualNodeViewModel> SelectedOperations { get; }
    public ObservableCollection<NodeTypeInfo> AvailableOperations { get; }

    public bool IsExecuting
    {
        get => _isExecuting;
        set => this.RaiseAndSetIfChanged(ref _isExecuting, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    // Commands
    public ICommand ExecuteGraphCommand { get; }
    public ICommand ClearGraphCommand { get; }
    public ICommand CreateNodeCommand { get; }
    public ICommand DeleteSelectionCommand { get; }

    private void LoadAvailableOperations()
    {
        var nodeTypes = _nodeRegistry.GetAllNodeTypes().OrderBy(n => n.Category).ThenBy(n => n.DisplayName);
        foreach (var nodeType in nodeTypes)
        {
            AvailableOperations.Add(nodeType);
        }
        
        _logger.LogInformation("Loaded {Count} available operations", AvailableOperations.Count);
    }

    public void CreateNode(string? nodeTypeName)
    {
        if (string.IsNullOrEmpty(nodeTypeName))
            return;

        try
        {
            var node = _nodeRegistry.CreateNode(nodeTypeName);
            var visualNode = new VisualNodeViewModel(node);
            
            // Position new nodes nicely
            var location = GetNextNodePosition();
            visualNode.Location = location;

            Operations.Add(visualNode);
            StatusText = $"Created {node.Name}";

            _logger.LogDebug("Created node: {NodeType} at ({X}, {Y})", nodeTypeName, location.X, location.Y);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create node: {NodeType}", nodeTypeName);
            StatusText = $"Failed to create {nodeTypeName}";
        }
    }

    public void CreateConnection(VisualPinViewModel source, VisualPinViewModel target)
    {
        try
        {
            if (!source.Pin.CanConnectTo(target.Pin))
            {
                StatusText = $"Cannot connect {source.Pin.DataType.Name} to {target.Pin.DataType.Name}";
                return;
            }

            var connection = new NodeConnection(source.Pin, target.Pin);
            var visualConnection = new VisualConnectionViewModel(connection, source, target);

            Connections.Add(visualConnection);
            StatusText = $"Connected nodes";

            _logger.LogDebug("Created connection between pins");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connection");
            StatusText = "Connection failed";
        }
    }

    private async Task ExecuteGraphAsync()
    {
        if (IsExecuting) return;

        try
        {
            IsExecuting = true;
            StatusText = "Executing graph...";

            var nodes = Operations.Select(op => op.Node).ToList();
            
            var progress = new Progress<string>(message => StatusText = message);
            var result = await _executionService.ExecuteNodesAsync(nodes, progress: progress);

            var stats = _executionService.GetExecutionStatistics(result);
            StatusText = result.Success 
                ? $"✅ Execution completed: {stats}" 
                : $"❌ Execution failed: {result.Errors.Count} errors";

            _logger.LogInformation("Graph execution completed: {Success}, {Stats}", result.Success, stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph execution failed");
            StatusText = "❌ Execution failed";
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private void ClearGraph()
    {
        try
        {
            foreach (var connection in Connections.ToList())
            {
                connection.Connection.Disconnect();
            }
            Connections.Clear();

            foreach (var node in Operations.ToList())
            {
                node.Dispose();
            }
            Operations.Clear();
            SelectedOperations.Clear();

            StatusText = "Graph cleared";
            _logger.LogDebug("Graph cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear graph");
            StatusText = "❌ Failed to clear graph";
        }
    }

    private void DeleteSelection()
    {
        try
        {
            var nodesToDelete = SelectedOperations.ToList();
            
            // Remove connections involving selected nodes
            var connectionsToRemove = Connections
                .Where(c => nodesToDelete.Any(n => n.Input.Contains(c.Input) || n.Output.Contains(c.Output)))
                .ToList();

            foreach (var connection in connectionsToRemove)
            {
                connection.Connection.Disconnect();
                Connections.Remove(connection);
            }

            // Remove nodes
            foreach (var node in nodesToDelete)
            {
                Operations.Remove(node);
                node.Dispose();
            }
            
            SelectedOperations.Clear();
            StatusText = $"Deleted {nodesToDelete.Count} node(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete selection");
            StatusText = "❌ Delete failed";
        }
    }

    private Point GetNextNodePosition()
    {
        const double gridSize = 200;
        const double startX = 100;
        const double startY = 100;

        int count = Operations.Count;
        int row = count / 4;
        int col = count % 4;

        return new Point(startX + col * gridSize, startY + row * gridSize);
    }

    private void UpdateCanExecuteCommands()
    {
        ((RelayCommand)ExecuteGraphCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearGraphCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DeleteSelectionCommand).RaiseCanExecuteChanged();
    }
}