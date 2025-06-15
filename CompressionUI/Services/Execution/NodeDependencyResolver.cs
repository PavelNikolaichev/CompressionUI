using CompressionUI.Models.Nodes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompressionUI.Services.Execution;

/// <summary>
/// Resolves node execution dependencies using topological sorting
/// </summary>
public class NodeDependencyResolver
{
    private readonly ILogger<NodeDependencyResolver> _logger;

    public NodeDependencyResolver(ILogger<NodeDependencyResolver> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sorts nodes in execution order based on their dependencies
    /// </summary>
    public List<INode> ResolveExecutionOrder(IEnumerable<INode> nodes)
    {
        var nodeList = nodes.ToList();
        var dependencies = BuildDependencyGraph(nodeList);
        
        return TopologicalSort(nodeList, dependencies);
    }

    /// <summary>
    /// Builds a dependency graph showing which nodes depend on which other nodes
    /// </summary>
    private Dictionary<string, HashSet<string>> BuildDependencyGraph(List<INode> nodes)
    {
        var dependencies = new Dictionary<string, HashSet<string>>();
        
        // Initialize empty dependency sets
        foreach (var node in nodes)
        {
            dependencies[node.Id] = new HashSet<string>();
        }

        // Build dependencies based on connections
        foreach (var node in nodes)
        {
            foreach (var inputPin in node.InputPins)
            {
                foreach (var connection in inputPin.Connections)
                {
                    var sourceNodeId = connection.Source.Owner.Id;
                    if (dependencies.ContainsKey(node.Id))
                    {
                        dependencies[node.Id].Add(sourceNodeId);
                    }
                }
            }
        }

        return dependencies;
    }

    /// <summary>
    /// Performs topological sort using Kahn's algorithm
    /// </summary>
    private List<INode> TopologicalSort(List<INode> nodes, Dictionary<string, HashSet<string>> dependencies)
    {
        var result = new List<INode>();
        var nodeMap = nodes.ToDictionary(n => n.Id, n => n);
        var inDegree = new Dictionary<string, int>();
        
        // Calculate in-degrees
        foreach (var node in nodes)
        {
            inDegree[node.Id] = dependencies[node.Id].Count;
        }

        // Find nodes with no dependencies
        var queue = new Queue<string>();
        foreach (var nodeId in inDegree.Keys)
        {
            if (inDegree[nodeId] == 0)
            {
                queue.Enqueue(nodeId);
            }
        }

        // Process nodes
        while (queue.Count > 0)
        {
            var currentNodeId = queue.Dequeue();
            var currentNode = nodeMap[currentNodeId];
            result.Add(currentNode);

            // Update in-degrees of dependent nodes
            foreach (var outputPin in currentNode.OutputPins)
            {
                foreach (var connection in outputPin.Connections)
                {
                    var dependentNodeId = connection.Target.Owner.Id;
                    if (inDegree.ContainsKey(dependentNodeId))
                    {
                        inDegree[dependentNodeId]--;
                        if (inDegree[dependentNodeId] == 0)
                        {
                            queue.Enqueue(dependentNodeId);
                        }
                    }
                }
            }
        }

        // Check for cycles
        if (result.Count != nodes.Count)
        {
            var cycleNodes = nodes.Where(n => !result.Contains(n)).ToList();
            var cycleNodeNames = string.Join(", ", cycleNodes.Select(n => n.Name));
            
            _logger.LogWarning("Circular dependency detected involving nodes: {CycleNodes}", cycleNodeNames);
            
            throw new InvalidOperationException($"Circular dependency detected involving nodes: {cycleNodeNames}");
        }

        _logger.LogDebug("Resolved execution order: {ExecutionOrder}", 
            string.Join(" → ", result.Select(n => n.Name)));

        return result;
    }

    /// <summary>
    /// Validates that all node dependencies can be satisfied
    /// </summary>
    public List<string> ValidateDependencies(IEnumerable<INode> nodes)
    {
        var errors = new List<string>();
        var nodeIds = nodes.Select(n => n.Id).ToHashSet();

        foreach (var node in nodes)
        {
            // Check required input connections
            foreach (var inputPin in node.InputPins.Where(p => p.IsRequired))
            {
                if (!inputPin.IsConnected && inputPin.Value == null)
                {
                    errors.Add($"Node '{node.Name}' has unconnected required input '{inputPin.Name}'");
                }
                
                // Check that connected nodes are in the execution set
                foreach (var connection in inputPin.Connections)
                {
                    var sourceNodeId = connection.Source.Owner.Id;
                    if (!nodeIds.Contains(sourceNodeId))
                    {
                        errors.Add($"Node '{node.Name}' depends on node '{connection.Source.Owner.Name}' which is not in the execution set");
                    }
                }
            }
        }

        return errors;
    }
}