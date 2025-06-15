using CompressionUI.Models.Nodes;
using Microsoft.Extensions.Logging;

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
        // TODO: add connections between nodes for all the templates
        return templateName.ToLower() switch
        {
            "text-processing" => CreateTextProcessingTemplate(),
            "image-loading" => CreateImageLoadingTemplate(),
            "simple-math" => CreateSimpleMathTemplate(),
            "pytorch-test" => CreatePyTorchTestTemplate(),
            "inference" => CreateInferencePipelineTemplate(),
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
        // Create nodes
        var numberA = CreateNodeWithProperties("VariableNode", new Dictionary<string, object>
        {
            ["valueType"] = "Float",
            ["value"] = "10.5"
        }, 100, 100);
        
        var numberB = CreateNodeWithProperties("VariableNode", new Dictionary<string, object>
        {
            ["valueType"] = "Float",
            ["value"] = "5.2"
        }, 100, 200);
        
        var arithmetic = CreateNodeWithProperties("ArithmeticNode", new Dictionary<string, object>
        {
            ["operation"] = "Add"
        }, 300, 150);
        
        var debugPrint = CreateNode("DebugPrintNode", 500, 150);
        
        // Create connections
        // Connect first variable to arithmetic input A
        var conn1 = new NodeConnection(
            numberA.GetOutputPin("output"), 
            arithmetic.GetInputPin("a")
        );
        
        // Connect second variable to arithmetic input B
        var conn2 = new NodeConnection(
            numberB.GetOutputPin("output"), 
            arithmetic.GetInputPin("b")
        );
        
        // Connect arithmetic output to debug print input
        var conn3 = new NodeConnection(
            arithmetic.GetOutputPin("result"), 
            debugPrint.GetInputPin("input")
        );
        
        return new List<INode> { numberA, numberB, arithmetic, debugPrint };
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
    
    private List<INode> CreateInferencePipelineTemplate()
    {
        return new List<INode>
        {
            // Load dataset
            CreateNodeWithProperties("DatasetNode", new Dictionary<string, object>
            {
                ["datasetPath"] = "test_data/",
                ["datasetType"] = "ImageFolder"
            }, 100, 100),
        
            // Load trained model
            CreateNodeWithProperties("PyTorchModelNode", new Dictionary<string, object>
            {
                ["modelPath"] = "trained_model.pt",
                ["device"] = "auto"
            }, 100, 250),
        
            // Run optimized inference
            CreateNodeWithProperties("InferenceNode", new Dictionary<string, object>
            {
                ["device"] = "auto",
                ["batchSize"] = 8,
                ["optimize"] = true,
                ["outputFormat"] = "probabilities"
            }, 400, 175),
        
            // Debug output
            CreateNode("DebugPrintNode", 700, 175),
        
            // Memory cleanup
            CreateNode("MemoryCleanupNode", 700, 300)
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
            ["pytorch-test"] = "PyTorch Model Testing",
            ["inference"] = "Inference Pipeline",
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