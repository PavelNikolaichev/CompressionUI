using CompressionUI.Models.Nodes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CompressionUI.Services;

/// <summary>
/// Service for serializing and deserializing node graphs
/// </summary>
public class NodeSerializationService
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly ILogger<NodeSerializationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public NodeSerializationService(
        INodeRegistry nodeRegistry,
        ILogger<NodeSerializationService> logger)
    {
        _nodeRegistry = nodeRegistry;
        _logger = logger;
        
        // Configure JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// Serialize a collection of nodes to a NodeGraph
    /// </summary>
    public NodeGraph SerializeNodes(IEnumerable<INode> nodes, string name = "Untitled Graph", string description = "")
    {
        var nodeList = nodes.ToList();
        var graph = new NodeGraph
        {
            Name = name,
            Description = description
        };

        _logger.LogDebug("Serializing {NodeCount} nodes to graph '{GraphName}'", nodeList.Count, name);

        try
        {
            // Serialize nodes
            foreach (var node in nodeList)
            {
                var serializedNode = SerializeNode(node);
                graph.Nodes.Add(serializedNode);
            }

            // Serialize connections
            var connections = ExtractConnections(nodeList);
            foreach (var connection in connections)
            {
                var serializedConnection = SerializeConnection(connection);
                graph.Connections.Add(serializedConnection);
            }

            graph.Touch();
            _logger.LogInformation("Successfully serialized graph with {NodeCount} nodes and {ConnectionCount} connections",
                graph.Nodes.Count, graph.Connections.Count);

            return graph;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize nodes to graph");
            throw;
        }
    }

    /// <summary>
    /// Deserialize a NodeGraph back to actual nodes
    /// </summary>
    public List<INode> DeserializeNodes(NodeGraph graph)
    {
        _logger.LogDebug("Deserializing graph '{GraphName}' with {NodeCount} nodes", graph.Name, graph.Nodes.Count);

        try
        {
            var nodes = new List<INode>();
            var nodeMap = new Dictionary<string, INode>();

            // First pass: Create all nodes
            foreach (var serializedNode in graph.Nodes)
            {
                var node = DeserializeNode(serializedNode);
                if (node != null)
                {
                    nodes.Add(node);
                    nodeMap[serializedNode.Id] = node;
                }
            }

            // Second pass: Restore connections
            foreach (var serializedConnection in graph.Connections)
            {
                RestoreConnection(serializedConnection, nodeMap);
            }

            _logger.LogInformation("Successfully deserialized graph with {NodeCount} nodes and {ConnectionCount} connections",
                nodes.Count, graph.Connections.Count);

            return nodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize graph '{GraphName}'", graph.Name);
            throw;
        }
    }

    /// <summary>
    /// Save a node graph to a JSON file
    /// </summary>
    public async Task SaveGraphToFileAsync(NodeGraph graph, string filePath)
    {
        _logger.LogDebug("Saving graph '{GraphName}' to file: {FilePath}", graph.Name, filePath);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(graph, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            _logger.LogInformation("Successfully saved graph '{GraphName}' to {FilePath}", graph.Name, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save graph to file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Load a node graph from a JSON file
    /// </summary>
    public async Task<NodeGraph> LoadGraphFromFileAsync(string filePath)
    {
        _logger.LogDebug("Loading graph from file: {FilePath}", filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Graph file not found: {filePath}");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var graph = JsonSerializer.Deserialize<NodeGraph>(json, _jsonOptions);

            if (graph == null)
            {
                throw new InvalidOperationException("Failed to deserialize graph from JSON");
            }

            _logger.LogInformation("Successfully loaded graph '{GraphName}' from {FilePath}", graph.Name, filePath);
            return graph;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load graph from file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Save nodes directly to a file
    /// </summary>
    public async Task SaveNodesToFileAsync(IEnumerable<INode> nodes, string filePath, string name = "Saved Graph")
    {
        var graph = SerializeNodes(nodes, name);
        await SaveGraphToFileAsync(graph, filePath);
    }

    /// <summary>
    /// Load nodes directly from a file
    /// </summary>
    public async Task<List<INode>> LoadNodesFromFileAsync(string filePath)
    {
        var graph = await LoadGraphFromFileAsync(filePath);
        return DeserializeNodes(graph);
    }

    /// <summary>
    /// Get supported file extensions
    /// </summary>
    public string[] SupportedExtensions => new[] { ".json", ".cgraph" };

    /// <summary>
    /// Get file filter for file dialogs
    /// </summary>
    public string FileFilter => "CompressionUI Graphs (*.cgraph)|*.cgraph|JSON files (*.json)|*.json|All files (*.*)|*.*";

    // Private helper methods

    private SerializableNode SerializeNode(INode node)
    {
        var serializedNode = new SerializableNode
        {
            Id = node.Id,
            NodeType = node.GetType().Name,
            Name = node.Name,
            Description = node.Description,
            Category = node.Category,
            X = node.X,
            Y = node.Y,
            Properties = node.Serialize()
        };

        // Serialize pins
        foreach (var pin in node.InputPins)
        {
            serializedNode.InputPins.Add(SerializePin(pin));
        }

        foreach (var pin in node.OutputPins)
        {
            serializedNode.OutputPins.Add(SerializePin(pin));
        }

        return serializedNode;
    }

    private SerializablePin SerializePin(NodePin pin)
    {
        return new SerializablePin
        {
            Id = pin.Id,
            Name = pin.Name,
            DataType = pin.DataType.Name,
            Direction = pin.Direction.ToString(),
            IsRequired = pin.IsRequired,
            Value = pin.Value
        };
    }

    private SerializableConnection SerializeConnection(NodeConnection connection)
    {
        return new SerializableConnection
        {
            Id = connection.Id,
            SourceNodeId = connection.Source.Owner.Id,
            SourcePinId = connection.Source.Id,
            TargetNodeId = connection.Target.Owner.Id,
            TargetPinId = connection.Target.Id
        };
    }

    private INode? DeserializeNode(SerializableNode serializedNode)
    {
        try
        {
            var node = _nodeRegistry.TryCreateNode(serializedNode.NodeType);
            if (node == null)
            {
                _logger.LogWarning("Failed to create node of type: {NodeType}", serializedNode.NodeType);
                return null;
            }

            // Restore basic properties
            node.Name = serializedNode.Name;
            node.Description = serializedNode.Description;
            node.X = serializedNode.X;
            node.Y = serializedNode.Y;

            // Restore node-specific properties
            node.Deserialize(serializedNode.Properties);

            // Restore pin values (connections will be restored separately)
            RestorePinValues(node, serializedNode);

            return node;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize node: {NodeType} ({NodeId})", 
                serializedNode.NodeType, serializedNode.Id);
            return null;
        }
    }

    private void RestorePinValues(INode node, SerializableNode serializedNode)
    {
        // Restore input pin values
        foreach (var serializedPin in serializedNode.InputPins)
        {
            var pin = node.GetInputPin(serializedPin.Id);
            if (pin != null && serializedPin.Value != null)
            {
                pin.Value = serializedPin.Value;
            }
        }

        // Restore output pin values
        foreach (var serializedPin in serializedNode.OutputPins)
        {
            var pin = node.GetOutputPin(serializedPin.Id);
            if (pin != null && serializedPin.Value != null)
            {
                pin.Value = serializedPin.Value;
            }
        }
    }

    private void RestoreConnection(SerializableConnection serializedConnection, Dictionary<string, INode> nodeMap)
    {
        try
        {
            if (!nodeMap.TryGetValue(serializedConnection.SourceNodeId, out var sourceNode) ||
                !nodeMap.TryGetValue(serializedConnection.TargetNodeId, out var targetNode))
            {
                _logger.LogWarning("Failed to restore connection: source or target node not found");
                return;
            }

            var sourcePin = sourceNode.GetOutputPin(serializedConnection.SourcePinId);
            var targetPin = targetNode.GetInputPin(serializedConnection.TargetPinId);

            if (sourcePin == null || targetPin == null)
            {
                _logger.LogWarning("Failed to restore connection: source or target pin not found");
                return;
            }

            // Create the connection
            var connection = new NodeConnection(sourcePin, targetPin);
            _logger.LogDebug("Restored connection: {SourceNode}.{SourcePin} → {TargetNode}.{TargetPin}",
                sourceNode.Name, sourcePin.Name, targetNode.Name, targetPin.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore connection: {ConnectionId}", serializedConnection.Id);
        }
    }

    private List<NodeConnection> ExtractConnections(List<INode> nodes)
    {
        var connections = new List<NodeConnection>();
        var seenConnections = new HashSet<string>();

        foreach (var node in nodes)
        {
            foreach (var pin in node.InputPins.Concat(node.OutputPins))
            {
                foreach (var connection in pin.Connections)
                {
                    // Avoid duplicate connections
                    if (!seenConnections.Contains(connection.Id))
                    {
                        connections.Add(connection);
                        seenConnections.Add(connection.Id);
                    }
                }
            }
        }

        return connections;
    }
}