using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes.Math;

/// <summary>
/// Performs tensor operations for ML workflows
/// </summary>
public class TensorOperationNode : NodeBase
{
    public override string Category => NodeCategories.Math;

    private NodePin _inputAPin = null!;
    private NodePin _inputBPin = null!;
    private NodePin _resultPin = null!;
    private NodePin _shapePin = null!;
    
    private NodeProperty _operationProperty = null!;
    private NodeProperty _axisProperty = null!;

    public TensorOperationNode(ILogger<TensorOperationNode>? logger = null) : base(logger)
    {
        Name = "Tensor Operation";
        Description = "Performs tensor operations (placeholder for real tensor library)";
        InitializePinsAndProperties();
    }

    protected override void InitializePinsAndProperties()
    {
        _inputAPin = AddInputPin("tensorA", "Tensor A", DataTypes.Tensor);
        _inputBPin = AddInputPin("tensorB", "Tensor B", DataTypes.Tensor, isRequired: false);
        
        _resultPin = AddOutputPin("result", "Result", DataTypes.Tensor);
        _shapePin = AddOutputPin("shape", "Shape", DataTypes.String);
        
        _operationProperty = AddProperty("operation", "Operation", PropertyType.Enum, "MatMul");
        _operationProperty.EnumValues = new[] { 
            "MatMul", "Add", "Subtract", "Multiply", "Divide", 
            "Transpose", "Reshape", "Sum", "Mean", "Max", "Min" 
        };
        
        _axisProperty = AddProperty("axis", "Axis", PropertyType.Integer, -1);
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context)
    {
        var operation = GetPropertyValue<string>("operation") ?? "MatMul";
        var axis = GetPropertyValue<int>("axis");
        
        var tensorA = _inputAPin.Value;
        var tensorB = _inputBPin.Value;

        try
        {
            context.ReportProgress($"Performing tensor operation: {operation}");

            // Placeholder tensor operations - in a real implementation, this would use actual tensor libraries
            var result = operation switch
            {
                "MatMul" when tensorB != null => $"MatMul({tensorA}, {tensorB})",
                "Add" when tensorB != null => $"Add({tensorA}, {tensorB})",
                "Subtract" when tensorB != null => $"Sub({tensorA}, {tensorB})",
                "Multiply" when tensorB != null => $"Mul({tensorA}, {tensorB})",
                "Divide" when tensorB != null => $"Div({tensorA}, {tensorB})",
                "Transpose" => $"Transpose({tensorA})",
                "Sum" => axis >= 0 ? $"Sum({tensorA}, axis={axis})" : $"Sum({tensorA})",
                "Mean" => axis >= 0 ? $"Mean({tensorA}, axis={axis})" : $"Mean({tensorA})",
                "Max" => axis >= 0 ? $"Max({tensorA}, axis={axis})" : $"Max({tensorA})",
                "Min" => axis >= 0 ? $"Min({tensorA}, axis={axis})" : $"Min({tensorA})",
                _ => $"{operation}({tensorA})"
            };

            _resultPin.Value = result;
            _shapePin.Value = "Shape(placeholder)";

            context.ReportProgress($"Tensor operation completed: {operation}");
            return await Task.FromResult(NodeExecutionResult.Successful(TimeSpan.Zero));
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failed($"Tensor operation failed: {ex.Message}");
        }
    }

    public override bool CanExecute()
    {
        var operation = GetPropertyValue<string>("operation") ?? "";
        var requiresTwoInputs = new[] { "MatMul", "Add", "Subtract", "Multiply", "Divide" };
        
        if (requiresTwoInputs.Contains(operation))
        {
            return _inputAPin.IsConnected && _inputBPin.IsConnected;
        }
        
        return _inputAPin.IsConnected;
    }
}