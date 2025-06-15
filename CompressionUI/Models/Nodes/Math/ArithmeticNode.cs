using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes.Math;

/// <summary>
/// Performs basic arithmetic operations - useful for parameter calculations
/// </summary>
public class ArithmeticNode : NodeBase
{
    public override string Category => NodeCategories.Math;

    private NodePin _inputAPin = null!;
    private NodePin _inputBPin = null!;
    private NodePin _resultPin = null!;
    private NodeProperty _operationProperty = null!;

    public ArithmeticNode(ILogger<ArithmeticNode>? logger = null) : base(logger)
    {
        Name = "Arithmetic";
        Description = "Performs basic arithmetic operations";
        InitializePinsAndProperties();
    }

    protected override void InitializePinsAndProperties()
    {
        _inputAPin = AddInputPin("a", "A", DataTypes.Float);
        _inputBPin = AddInputPin("b", "B", DataTypes.Float);
        _resultPin = AddOutputPin("result", "Result", DataTypes.Float);
        
        _operationProperty = AddProperty("operation", "Operation", PropertyType.Enum, "Add");
        _operationProperty.EnumValues = new[] { "Add", "Subtract", "Multiply", "Divide", "Power", "Min", "Max" };
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context)
    {
        var a = _inputAPin.GetValue<float>();
        var b = _inputBPin.GetValue<float>();
        var operation = GetPropertyValue<string>("operation") ?? "Add";

        try
        {
            float result = operation switch
            {
                "Add" => a + b,
                "Subtract" => a - b,
                "Multiply" => a * b,
                "Divide" => b != 0 ? a / b : throw new DivideByZeroException("Cannot divide by zero"),
                "Power" => (float) System.Math.Pow(a, b),
                "Min" => System.Math.Min(a, b),
                "Max" => System.Math.Max(a, b),
                _ => throw new InvalidOperationException($"Unknown operation: {operation}")
            };

            _resultPin.Value = result;
            context.ReportProgress($"{a} {operation} {b} = {result}");

            return await Task.FromResult(NodeExecutionResult.Successful(TimeSpan.Zero));
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failed($"Arithmetic error: {ex.Message}");
        }
    }
}