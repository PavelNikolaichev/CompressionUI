using CompressionUI.Models.Nodes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompressionUI.Services;

/// <summary>
/// High-level service for creating and managing nodes with templates
/// </summary>
public class NodeFactory
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly ILogger<NodeFactory> _logger;

    public NodeFactory(INodeRegistry nodeRegistry, ILogger<NodeFactory> logger)
    {
        _nodeRegistry = nodeRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Create a node with default configuration
    /// </summary>
    public INode CreateNode(string nodeTypeName, double x = 0, double y = 0)
    {
        var node = _nodeRegistry.CreateNode(nodeTypeName);
        node.X = x;
        node.Y = y;
        
        _logger.LogDebug("Created node {NodeType} at ({X}, {Y})", nodeTypeName, x, y);
        return node;
    }

    /// <summary>
    /// Create a node with property values
    /// </summary>
    public INode CreateNodeWithProperties(string nodeTypeName, Dictionary<string, object> properties, double x = 0, double y = 0)
    {
        var node = CreateNode(nodeTypeName, x, y);
        
        foreach (var kvp in properties)
        {
            try
            {
                node.SetPropertyValue(kvp.Key, kvp.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set property {PropertyName} = {Value} on node {NodeType}", 
                    kvp.Key, kvp.Value, nodeTypeName);
            }
        }
        
        return node;
    }

    /// <summary>
    /// Create a pre-configured workflow template
    /// </summary>
    public List<INode> CreateWorkflowTemplate(string templateName)
    {
        return templateName.ToLower() switch
        {
            "text-processing" => CreateTextProcessingTemplate(),
            "image-loading" => CreateImageLoadingTemplate(),
            "simple-math" => CreateSimpleMathTemplate(),
            "pytorch-test" => CreatePyTorchTestTemplate(),
            _ => throw new ArgumentException($"Unknown template: {templateName}")
        };
    }

    private List<INode> CreateTextProcessingTemplate()
    {
        return new List<INode>
        {
            CreateNodeWithProperties("TextDataLoaderNode", new Dictionary<string, object>
            {
                ["filePath"] = "sample.txt"
            }, 100, 100),
            
            CreateNode("DebugPrintNode", 300, 100),
            
            CreateNodeWithProperties("VariableNode", new Dictionary<string, object>
            {
                ["valueType"] = "String",
                ["value"] = "Text processing complete"
            }, 500, 100)
        };
    }

    private List<INode> CreateImageLoadingTemplate()
    {
        return new List<INode>
        {
            CreateNodeWithProperties("ImageDataLoaderNode", new Dictionary<string, object>
            {
                ["filePath"] = "sample.jpg"
            }, 100, 100),
            
            CreateNode("DebugPrintNode", 300, 100)
        };
    }

    private List<INode> CreateSimpleMathTemplate()
    {
        return new List<INode>
        {
            CreateNodeWithProperties("VariableNode", new Dictionary<string, object>
            {
                ["valueType"] = "Float",
                ["value"] = "10.5"
            }, 100, 100),
            
            CreateNodeWithProperties("VariableNode", new Dictionary<string, object>
            {
                ["valueType"] = "Float",
                ["value"] = "5.2"
            }, 100, 200),
            
            CreateNodeWithProperties("ArithmeticNode", new Dictionary<string, object>
            {
                ["operation"] = "Add"
            }, 300, 150),
            
            CreateNode("DebugPrintNode", 500, 150)
        };
    }

    private List<INode> CreatePyTorchTestTemplate()
    {
        return new List<INode>
        {
            CreateNodeWithProperties("PyTorchModelNode", new Dictionary<string, object>
            {
                ["modelPath"] = "model.pt",
                ["device"] = "cpu"
            }, 100, 100),
            
            CreateNode("DebugPrintNode", 300, 100),
            CreateNode("MemoryCleanupNode", 500, 100)
        };
    }

    /// <summary>
    /// Get available workflow templates
    /// </summary>
    public Dictionary<string, string> GetAvailableTemplates()
    {
        return new Dictionary<string, string>
        {
            ["text-processing"] = "Text Processing Workflow",
            ["image-loading"] = "Image Loading Workflow", 
            ["simple-math"] = "Simple Math Operations",
            ["pytorch-test"] = "PyTorch Model Testing"
        };
    }

    /// <summary>
    /// Connect two nodes by pin IDs
    /// </summary>
    public NodeConnection? ConnectNodes(INode sourceNode, string outputPinId, INode targetNode, string inputPinId)
    {
        var sourcePin = sourceNode.GetOutputPin(outputPinId);
        var targetPin = targetNode.GetInputPin(inputPinId);

        if (sourcePin == null)
        {
            _logger.LogWarning("Output pin {PinId} not found on node {NodeName}", outputPinId, sourceNode.Name);
            return null;
        }

        if (targetPin == null)
        {
            _logger.LogWarning("Input pin {PinId} not found on node {NodeName}", inputPinId, targetNode.Name);
            return null;
        }

        if (!sourcePin.CanConnectTo(targetPin))
        {
            _logger.LogWarning("Cannot connect {SourcePin} to {TargetPin}", sourcePin, targetPin);
            return null;
        }

        try
        {
            var connection = new NodeConnection(sourcePin, targetPin);
            _logger.LogDebug("Connected {SourcePin} to {TargetPin}", sourcePin, targetPin);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect {SourcePin} to {TargetPin}", sourcePin, targetPin);
            return null;
        }
    }
}