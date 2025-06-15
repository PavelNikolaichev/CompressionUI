using System;

namespace CompressionUI.Models.Nodes;

/// <summary>
/// Represents a connection between two node pins
/// </summary>
public class NodeConnection
{
    public string Id { get; }
    public NodePin Source { get; }
    public NodePin Target { get; }
    public DateTime CreatedAt { get; }

    public NodeConnection(NodePin source, NodePin target)
    {
        if (!source.CanConnectTo(target))
        {
            throw new InvalidOperationException($"Cannot connect {source} to {target}");
        }

        Id = Guid.NewGuid().ToString();
        Source = source;
        Target = target;
        CreatedAt = DateTime.UtcNow;

        // Add connection to both pins
        Source.AddConnection(this);
        Target.AddConnection(this);
    }

    public void Disconnect()
    {
        Source.RemoveConnection(this);
        Target.RemoveConnection(this);
    }

    public void TransferData()
    {
        Target.Value = Source.Value;
    }

    public override string ToString() => $"{Source} -> {Target}";
}