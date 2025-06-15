using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes.Utility;

/// <summary>
/// Stores and outputs a configurable value - useful for constants and parameters
/// </summary>
public class VariableNode : NodeBase
{
    public override string Category => NodeCategories.Utility;

    private NodePin _outputPin = null!;
    private NodeProperty _valueTypeProperty = null!;
    private NodeProperty _valueProperty = null!;

    public VariableNode(ILogger<VariableNode>? logger = null) : base(logger)
    {
        Name = "Variable";
        Description = "Outputs a configurable constant value";
        InitializePinsAndProperties();
    }

    protected override void InitializePinsAndProperties()
    {
        // Output pin - type depends on the selected value type
        _outputPin = AddOutputPin("output", "Value", DataTypes.Any);
        
        // Properties
        _valueTypeProperty = AddProperty("valueType", "Value Type", PropertyType.Enum, "String");
        _valueTypeProperty.EnumValues = new[] { "String", "Integer", "Float", "Boolean" };
        
        _valueProperty = AddProperty("value", "Value", PropertyType.String, "");
        
        // Update output pin type when value type changes
        _valueTypeProperty.PropertyChanged += (_, _) => UpdateOutputPinType();
    }

    private void UpdateOutputPinType()
    {
        var valueType = GetPropertyValue<string>("valueType");
        _outputPin.DataType = valueType switch
        {
            "Integer" => DataTypes.Integer,
            "Float" => DataTypes.Float,
            "Boolean" => DataTypes.Boolean,
            _ => DataTypes.String
        };
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context)
    {
        var valueType = GetPropertyValue<string>("valueType");
        var rawValue = GetPropertyValue<string>("value") ?? "";

        try
        {
            object? typedValue = valueType switch
            {
                "Integer" => int.Parse(rawValue),
                "Float" => float.Parse(rawValue),
                "Boolean" => bool.Parse(rawValue),
                _ => rawValue
            };

            _outputPin.Value = typedValue;
            context.ReportProgress($"Variable output: {typedValue}");

            return await Task.FromResult(NodeExecutionResult.Successful(TimeSpan.Zero));
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failed($"Invalid {valueType.ToLower()} value: '{rawValue}' - {ex.Message}");
        }
    }
}