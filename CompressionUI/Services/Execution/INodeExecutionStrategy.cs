using CompressionUI.Models.Nodes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CompressionUI.Services.Execution;

/// <summary>
/// Strategy for executing a collection of nodes
/// </summary>
public interface INodeExecutionStrategy
{
    string Name { get; }
    string Description { get; }
    
    Task<NodeGraphExecutionResult> ExecuteAsync(
        IEnumerable<INode> nodes, 
        NodeExecutionContext context);
}

/// <summary>
/// Result of executing a node graph
/// </summary>
public class NodeGraphExecutionResult
{
    public bool Success { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public int NodesExecuted { get; set; }
    public int NodesSkipped { get; set; }
    public int NodesFailed { get; set; }
    public List<NodeExecutionError> Errors { get; set; } = new();
    public Dictionary<string, object> OutputData { get; set; } = new();
    
    public static NodeGraphExecutionResult Successful(TimeSpan executionTime, int nodesExecuted) =>
        new() 
        { 
            Success = true, 
            TotalExecutionTime = executionTime, 
            NodesExecuted = nodesExecuted 
        };

    public static NodeGraphExecutionResult Failed(string error) =>
        new() 
        { 
            Success = false, 
            Errors = new List<NodeExecutionError> { new(error) }
        };
}

/// <summary>
/// Represents an error during node execution
/// </summary>
public class NodeExecutionError
{
    public string NodeId { get; set; } = "";
    public string NodeName { get; set; } = "";
    public string Message { get; set; } = "";
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public NodeExecutionError() { }

    public NodeExecutionError(string message)
    {
        Message = message;
    }

    public NodeExecutionError(INode node, string message, Exception? exception = null)
    {
        NodeId = node.Id;
        NodeName = node.Name;
        Message = message;
        Exception = exception;
    }

    public override string ToString() => 
        string.IsNullOrEmpty(NodeName) ? Message : $"{NodeName}: {Message}";
}