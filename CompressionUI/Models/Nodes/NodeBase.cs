using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;

namespace CompressionUI.Models.Nodes;

/// <summary>
/// Abstract base class providing common node functionality
/// </summary>
public abstract class NodeBase : INode, INotifyPropertyChanged, IDisposable
{
    private NodeExecutionState _state = NodeExecutionState.Idle;
    private string _name = "";
    private string _description = "";
    private double _x = 0;
    private double _y = 0;
    private bool _disposed = false;

    protected readonly ILogger? _logger;
    protected readonly List<NodePin> _inputPins = new();
    protected readonly List<NodePin> _outputPins = new();
    protected readonly List<NodeProperty> _properties = new();

    // INode implementation
    public string Id { get; }
    public abstract string Category { get; }
    public virtual Version Version => new(1, 0, 0);

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public NodeExecutionState State
    {
        get => _state;
        private set
        {
            var oldState = _state;
            if (SetField(ref _state, value))
            {
                StateChanged?.Invoke(this, new NodeStateChangedEventArgs(oldState, value));
            }
        }
    }

    public string? LastErrorMessage { get; private set; }
    public DateTime? LastExecutionTime { get; private set; }

    public double X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    // Collections
    public IReadOnlyList<NodePin> InputPins => _inputPins.AsReadOnly();
    public IReadOnlyList<NodePin> OutputPins => _outputPins.AsReadOnly();
    public IReadOnlyList<NodeProperty> Properties => _properties.AsReadOnly();

    // Events
    public event EventHandler<NodeStateChangedEventArgs>? StateChanged;
    public event EventHandler<NodePropertyChangedEventArgs>? PropertyChanged;
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }
    private PropertyChangedEventHandler? _propertyChanged;

    protected NodeBase(ILogger? logger = null)
    {
        Id = Guid.NewGuid().ToString();
        _logger = logger;
        _name = GetType().Name.Replace("Node", "");
        
        // Subscribe to property changes
        foreach (var property in _properties)
        {
            property.PropertyChanged += OnNodePropertyChanged;
        }
    }

    // Abstract methods that derived classes must implement
    protected abstract Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context);
    protected abstract void InitializePinsAndProperties();

    // Pin management
    protected NodePin AddInputPin(string id, string name, DataType dataType, bool isRequired = true)
    {
        var pin = new NodePin(id, name, dataType, PinDirection.Input, this)
        {
            IsRequired = isRequired
        };
        _inputPins.Add(pin);
        return pin;
    }

    protected NodePin AddOutputPin(string id, string name, DataType dataType, bool allowMultiple = true)
    {
        var pin = new NodePin(id, name, dataType, PinDirection.Output, this)
        {
            AllowMultipleConnections = allowMultiple
        };
        _outputPins.Add(pin);
        return pin;
    }

    // Property management
    protected NodeProperty AddProperty(string id, string name, PropertyType type, object? defaultValue = null)
    {
        var property = new NodeProperty(id, name, type, defaultValue);
        property.PropertyChanged += OnNodePropertyChanged;
        _properties.Add(property);
        return property;
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is NodeProperty property)
        {
            PropertyChanged?.Invoke(this, new NodePropertyChangedEventArgs(
                property.Id, null, property.Value));
        }
    }

    // INode interface implementation
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        if (State == NodeExecutionState.Executing)
        {
            return NodeExecutionResult.Failed("Node is already executing");
        }

        if (!CanExecute())
        {
            return NodeExecutionResult.Failed("Node cannot execute - validation failed");
        }

        var stopwatch = Stopwatch.StartNew();
        State = NodeExecutionState.Executing;
        LastErrorMessage = null;

        try
        {
            context.ThrowIfCancellationRequested();
            context.ReportProgress($"Executing {Name}...");

            // Transfer input data from connected pins
            TransferInputData();

            var result = await ExecuteInternalAsync(context);
            
            if (result.Success)
            {
                State = NodeExecutionState.Completed;
                context.ReportProgress($"{Name} completed successfully");
            }
            else
            {
                State = NodeExecutionState.Error;
                LastErrorMessage = result.ErrorMessage;
                _logger?.LogError("Node {NodeName} execution failed: {Error}", Name, result.ErrorMessage);
            }

            result.ExecutionTime = stopwatch.Elapsed;
            LastExecutionTime = DateTime.UtcNow;
            return result;
        }
        catch (OperationCanceledException)
        {
            State = NodeExecutionState.Cancelled;
            _logger?.LogInformation("Node {NodeName} execution was cancelled", Name);
            return NodeExecutionResult.Failed("Execution was cancelled");
        }
        catch (Exception ex)
        {
            State = NodeExecutionState.Error;
            LastErrorMessage = ex.Message;
            _logger?.LogError(ex, "Node {NodeName} execution failed with exception", Name);
            return NodeExecutionResult.Failed(ex.Message, ex);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private void TransferInputData()
    {
        foreach (var inputPin in _inputPins)
        {
            if (inputPin.IsConnected)
            {
                // Get data from the first connected source pin
                var connection = inputPin.Connections.FirstOrDefault();
                if (connection != null)
                {
                    connection.TransferData();
                }
            }
        }
    }

    public virtual void Reset()
    {
        State = NodeExecutionState.Idle;
        LastErrorMessage = null;
        
        // Clear output pin values
        foreach (var pin in _outputPins)
        {
            pin.Value = null;
        }
    }

    public virtual bool CanExecute()
    {
        // Check if all required input pins are connected or have values
        var requiredInputs = _inputPins.Where(p => p.IsRequired);
        return requiredInputs.All(pin => pin.IsConnected || pin.Value != null);
    }

    // Pin lookup methods
    public NodePin? GetInputPin(string pinId) => _inputPins.FirstOrDefault(p => p.Id == pinId);
    public NodePin? GetOutputPin(string pinId) => _outputPins.FirstOrDefault(p => p.Id == pinId);
    public NodePin? GetPin(string pinId) => GetInputPin(pinId) ?? GetOutputPin(pinId);

    // Property methods
    public NodeProperty? GetProperty(string propertyId) => _properties.FirstOrDefault(p => p.Id == propertyId);

    public T? GetPropertyValue<T>(string propertyId)
    {
        var property = GetProperty(propertyId);
        if (property == null) return default;
        return property.GetValue<T>();
    }

    public void SetPropertyValue(string propertyId, object? value)
    {
        var property = GetProperty(propertyId);
        if (property != null)
        {
            property.Value = value;
        }
    }

    // Validation
    public virtual IEnumerable<string> ValidateConfiguration()
    {
        var errors = new List<string>();

        // Check required properties
        foreach (var property in _properties.Where(p => p.IsRequired))
        {
            if (property.Value == null)
            {
                errors.Add($"Required property '{property.Name}' is not set");
            }
        }

        // Check required input connections
        foreach (var pin in _inputPins.Where(p => p.IsRequired))
        {
            if (!pin.IsConnected && pin.Value == null)
            {
                errors.Add($"Required input '{pin.Name}' is not connected");
            }
        }

        return errors;
    }

    // Serialization
    public virtual Dictionary<string, object> Serialize()
    {
        var data = new Dictionary<string, object>
        {
            ["Id"] = Id,
            ["Name"] = Name,
            ["Description"] = Description,
            ["X"] = X,
            ["Y"] = Y,
            ["Properties"] = _properties.ToDictionary(p => p.Id, p => p.Value ?? "")
        };

        return data;
    }

    public virtual void Deserialize(Dictionary<string, object> data)
    {
        if (data.TryGetValue("Name", out var name)) Name = name.ToString() ?? "";
        if (data.TryGetValue("Description", out var desc)) Description = desc.ToString() ?? "";
        if (data.TryGetValue("X", out var x)) X = Convert.ToDouble(x);
        if (data.TryGetValue("Y", out var y)) Y = Convert.ToDouble(y);

        if (data.TryGetValue("Properties", out var propsObj) && 
            propsObj is Dictionary<string, object> props)
        {
            foreach (var kvp in props)
            {
                SetPropertyValue(kvp.Key, kvp.Value);
            }
        }
    }

    // Property change notification helper
    protected bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    // Disposal
    public virtual void Dispose()
    {
        if (_disposed) return;

        // Disconnect all pins
        var allConnections = _inputPins.Concat(_outputPins)
            .SelectMany(p => p.Connections)
            .Distinct()
            .ToList();

        foreach (var connection in allConnections)
        {
            connection.Disconnect();
        }

        // Unsubscribe from property events
        foreach (var property in _properties)
        {
            property.PropertyChanged -= OnNodePropertyChanged;
        }

        _disposed = true;
    }
}