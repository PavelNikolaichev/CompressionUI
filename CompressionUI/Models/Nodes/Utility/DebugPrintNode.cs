using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes.Utility;

/// <summary>
/// Prints input values to debug output - essential for testing workflows
/// </summary>
public class DebugPrintNode : NodeBase
{
    public override string Category => NodeCategories.Utility;

    private NodePin _inputPin = null!;
    private NodePin _outputPin = null!;
    private NodeProperty _prefixProperty = null!;

    public DebugPrintNode(ILogger<DebugPrintNode>? logger = null) : base(logger)
    {
        Name = "Debug Print";
        Description = "Prints input value to console and passes it through";
        InitializePinsAndProperties();
    }

    protected override void InitializePinsAndProperties()
    {
        // Input: accepts any data type
        _inputPin = AddInputPin("input", "Input", DataTypes.Any, isRequired: false);
        
        // Output: passes through the same value
        _outputPin = AddOutputPin("output", "Output", DataTypes.Any);
        
        // Property: optional prefix for the debug message
        _prefixProperty = AddProperty("prefix", "Prefix", PropertyType.String, "[DEBUG]");
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context)
    {
        var prefix = GetPropertyValue<string>("prefix") ?? "[DEBUG]";
        var inputValue = _inputPin.Value;
        
        // Create debug message
        var message = inputValue switch
        {
            null => $"{prefix} <null>",
            string str => $"{prefix} \"{str}\"",
            _ => $"{prefix} {inputValue} ({inputValue.GetType().Name})"
        };

        // Output to console and logger
        Console.WriteLine(message);
        _logger?.LogInformation("DebugPrint: {Message}", message);
        context.ReportProgress(message);

        // Pass through the value
        _outputPin.Value = inputValue;

        return await Task.FromResult(NodeExecutionResult.Successful(TimeSpan.Zero));
    }
}