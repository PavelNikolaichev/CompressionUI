using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes;

/// <summary>
/// Core interface that all nodes must implement
/// </summary>
public interface INode
{
    // Basic node information
    string Id { get; }
    string Name { get; set; }
    string Description { get; set; }
    string Category { get; }
    Version Version { get; }

    // Execution state
    NodeExecutionState State { get; }
    string? LastErrorMessage { get; }
    DateTime? LastExecutionTime { get; }

    // Node structure
    IReadOnlyList<NodePin> InputPins { get; }
    IReadOnlyList<NodePin> OutputPins { get; }
    IReadOnlyList<NodeProperty> Properties { get; }

    // Position for visual editor (can be ignored for headless execution)
    double X { get; set; }
    double Y { get; set; }

    // Events
    event EventHandler<NodeStateChangedEventArgs>? StateChanged;
    event EventHandler<NodePropertyChangedEventArgs>? PropertyChanged;

    // Core functionality
    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context);
    void Reset();
    bool CanExecute();
    
    // Pin management
    NodePin? GetInputPin(string pinId);
    NodePin? GetOutputPin(string pinId);
    NodePin? GetPin(string pinId);
    
    // Property management
    NodeProperty? GetProperty(string propertyId);
    T? GetPropertyValue<T>(string propertyId);
    void SetPropertyValue(string propertyId, object? value);
    
    // Validation
    IEnumerable<string> ValidateConfiguration();
    
    // Serialization support
    Dictionary<string, object> Serialize();
    void Deserialize(Dictionary<string, object> data);
    
    // Cleanup
    void Dispose();
}