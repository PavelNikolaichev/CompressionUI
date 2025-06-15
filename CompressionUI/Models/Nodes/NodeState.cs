using System;

namespace CompressionUI.Models.Nodes;

public enum NodeExecutionState
{
    Idle,
    Executing,
    Completed,
    Error,
    Cancelled
}

/// <summary>
/// Event arguments for node state changes
/// </summary>
public class NodeStateChangedEventArgs : EventArgs
{
    public NodeExecutionState OldState { get; }
    public NodeExecutionState NewState { get; }
    public string? Message { get; }

    public NodeStateChangedEventArgs(NodeExecutionState oldState, NodeExecutionState newState, string? message = null)
    {
        OldState = oldState;
        NewState = newState;
        Message = message;
    }
}

/// <summary>
/// Event arguments for node property changes
/// </summary>
public class NodePropertyChangedEventArgs : EventArgs
{
    public string PropertyId { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }

    public NodePropertyChangedEventArgs(string propertyId, object? oldValue, object? newValue)
    {
        PropertyId = propertyId;
        OldValue = oldValue;
        NewValue = newValue;
    }
}