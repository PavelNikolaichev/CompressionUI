using System;
using System.Collections.Generic;
using System.Linq;

namespace CompressionUI.Models.Nodes;

/// <summary>
/// Represents a complete node graph with nodes and connections
/// </summary>
public class NodeGraph
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Untitled Graph";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "1.0.0";
    
    public List<SerializableNode> Nodes { get; set; } = new();
    public List<SerializableConnection> Connections { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Update the modified timestamp
    /// </summary>
    public void Touch()
    {
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Get statistics about this graph
    /// </summary>
    public NodeGraphStatistics GetStatistics()
    {
        var nodesByCategory = Nodes.GroupBy(n => n.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        return new NodeGraphStatistics
        {
            TotalNodes = Nodes.Count,
            TotalConnections = Connections.Count,
            NodesByCategory = nodesByCategory,
            HasCycles = false // TODO: Implement cycle detection
        };
    }
}

/// <summary>
/// Serializable representation of a node
/// </summary>
public class SerializableNode
{
    public string Id { get; set; } = "";
    public string NodeType { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<SerializablePin> InputPins { get; set; } = new();
    public List<SerializablePin> OutputPins { get; set; } = new();
}

/// <summary>
/// Serializable representation of a pin
/// </summary>
public class SerializablePin
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public string Direction { get; set; } = "";
    public bool IsRequired { get; set; }
    public object? Value { get; set; }
}

/// <summary>
/// Serializable representation of a connection
/// </summary>
public class SerializableConnection
{
    public string Id { get; set; } = "";
    public string SourceNodeId { get; set; } = "";
    public string SourcePinId { get; set; } = "";
    public string TargetNodeId { get; set; } = "";
    public string TargetPinId { get; set; } = "";
}

/// <summary>
/// Statistics about a node graph
/// </summary>
public class NodeGraphStatistics
{
    public int TotalNodes { get; set; }
    public int TotalConnections { get; set; }
    public Dictionary<string, int> NodesByCategory { get; set; } = new();
    public bool HasCycles { get; set; }

    public override string ToString() =>
        $"Nodes: {TotalNodes}, Connections: {TotalConnections}, Categories: {NodesByCategory.Count}";
}