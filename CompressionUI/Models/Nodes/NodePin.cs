using System;
using System.Collections.Generic;

namespace CompressionUI.Models.Nodes;

public enum PinDirection
{
    Input,
    Output
}

/// <summary>
/// Represents an input or output connection point on a node
/// </summary>
public class NodePin
{
    public string Id { get; }
    public string Name { get; set; }
    public string Description { get; set; }
    // public string DisplayName { get; set; }
    public DataType DataType { get; set; }
    public PinDirection Direction { get; }
    public INode Owner { get; }
    public bool IsRequired { get; set; }
    public bool AllowMultipleConnections { get; set; }

    private readonly List<NodeConnection> _connections = new();
    public IReadOnlyList<NodeConnection> Connections => _connections.AsReadOnly();

    public bool IsConnected => _connections.Count > 0;
    
    private object? _value;
    public object? Value 
    { 
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    /// <summary>
    /// Event that fires when the pin value changes
    /// </summary>
    public event EventHandler? ValueChanged;

    public NodePin(string id, string name, DataType dataType, PinDirection direction, INode owner)
    {
        Id = id;
        Name = name;
        DataType = dataType;
        Direction = direction;
        Owner = owner;
        Description = "";
        IsRequired = direction == PinDirection.Input;
        AllowMultipleConnections = direction == PinDirection.Output;
    }

    public bool CanConnectTo(NodePin other)
    {
        if (other == this) return false;
        if (other.Owner == Owner) return false; // No self-connections
        if (Direction == other.Direction) return false; // Input to input, output to output not allowed
        
        // Check connection limits
        if (!AllowMultipleConnections && IsConnected) return false;
        if (!other.AllowMultipleConnections && other.IsConnected) return false;
        
        // Check data type compatibility
        var sourcePin = Direction == PinDirection.Output ? this : other;
        var targetPin = Direction == PinDirection.Input ? this : other;
        
        return targetPin.DataType.IsCompatibleWith(sourcePin.DataType);
    }

    internal void AddConnection(NodeConnection connection)
    {
        if (!_connections.Contains(connection))
        {
            _connections.Add(connection);
        }
    }

    internal void RemoveConnection(NodeConnection connection)
    {
        _connections.Remove(connection);
    }

    public T? GetValue<T>()
    {
        if (Value is T typedValue)
            return typedValue;
        
        if (Value != null && typeof(T).IsAssignableFrom(Value.GetType()))
            return (T)Value;
            
        return default(T);
    }

    public override string ToString() => $"{Owner.Name}.{Name} ({DataType.Name})";
}