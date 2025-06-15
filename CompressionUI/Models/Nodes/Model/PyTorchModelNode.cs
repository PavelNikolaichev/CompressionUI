using CompressionUI.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes.Model;

/// <summary>
/// Wraps PyTorch models for inference - core component for ML workflows
/// </summary>
public class PyTorchModelNode : NodeBase
{
    public override string Category => NodeCategories.Model;

    private readonly PythonService _pythonService;
    
    private NodePin _inputDataPin = null!;
    private NodePin _modelPathPin = null!;
    private NodePin _outputPin = null!;
    private NodePin _modelInfoPin = null!;
    
    private NodeProperty _modelPathProperty = null!;
    private NodeProperty _deviceProperty = null!;
    private NodeProperty _modelCodeProperty = null!;

    public PyTorchModelNode(PythonService pythonService, ILogger<PyTorchModelNode>? logger = null) : base(logger)
    {
        _pythonService = pythonService;
        Name = "PyTorch Model";
        Description = "Loads and runs PyTorch models for inference";
        InitializePinsAndProperties();
    }

    protected override void InitializePinsAndProperties()
    {
        _inputDataPin = AddInputPin("input", "Input Data", DataTypes.Tensor);
        _modelPathPin = AddInputPin("modelPath", "Model Path", DataTypes.FilePath, isRequired: false);
        
        _outputPin = AddOutputPin("output", "Model Output", DataTypes.Tensor);
        _modelInfoPin = AddOutputPin("modelInfo", "Model Info", DataTypes.String);
        
        _modelPathProperty = AddProperty("modelPath", "Model Path", PropertyType.FilePath, "");
        _modelPathProperty.FileFilter = "PyTorch models (*.pt;*.pth)|*.pt;*.pth|All files (*.*)|*.*";
        
        _deviceProperty = AddProperty("device", "Device", PropertyType.Enum, "cpu");
        _deviceProperty.EnumValues = new[] { "cpu", "cuda", "auto" };
        
        // Python code for model loading/inference (customizable)
        _modelCodeProperty = AddProperty("modelCode", "Custom Model Code", PropertyType.String, GetDefaultModelCode());
    }

    private string GetDefaultModelCode()
    {
        return @"
import torch
import torch.nn as nn

def load_model(model_path, device='cpu'):
    ""Load PyTorch model from file""
    model = torch.load(model_path, map_location=device)
    model.eval()
    return model

def run_inference(model, input_data, device='cpu'):
    ""Run model inference""
    with torch.no_grad():
        if isinstance(input_data, str):
            # Convert string representation to tensor if needed
            input_tensor = torch.tensor(eval(input_data))
        else:
            input_tensor = input_data
        
        input_tensor = input_tensor.to(device)
        output = model(input_tensor)
        return output
";
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context)
    {
        var modelPath = _modelPathPin.GetValue<string>() ?? GetPropertyValue<string>("modelPath");
        var device = GetPropertyValue<string>("device") ?? "cpu";
        var inputData = _inputDataPin.Value;

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return NodeExecutionResult.Failed("No model path specified");
        }

        try
        {
            context.ReportProgress("Loading PyTorch model...");

            // Prepare Python code for execution
            var pythonCode = $@"
{GetPropertyValue<string>("modelCode")}

# Set device
device = '{device}'
if device == 'auto':
    device = 'cuda' if torch.cuda.is_available() else 'cpu'

print(f'Using device: {{device}}')

# Load model
model_path = r'{modelPath}'
model = load_model(model_path, device)

# Get model info
model_info = f'Model loaded from {{model_path}}, Device: {{device}}'
print(model_info)

# Run inference if input data is provided
if hasattr(globals(), 'input_data') and input_data is not None:
    output = run_inference(model, input_data, device)
    print(f'Model output shape: {{output.shape if hasattr(output, ""shape"") else ""scalar""}}')
else:
    output = None
    print('No input data provided, model loaded only')
";

            // Execute Python code
            var result = await _pythonService.ExecutePythonCodeAsync(pythonCode);

            if (!result.Success)
            {
                return NodeExecutionResult.Failed($"PyTorch execution failed: {result.Error}");
            }

            // For now, we'll output the Python execution result
            // In a full implementation, you'd extract the actual tensor data
            _outputPin.Value = "PyTorch model output"; // Placeholder
            _modelInfoPin.Value = result.Output;

            context.ReportProgress("PyTorch model execution completed");
            return NodeExecutionResult.Successful(TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "PyTorch model execution failed");
            return NodeExecutionResult.Failed($"Model execution error: {ex.Message}");
        }
    }

    public override bool CanExecute()
    {
        var modelPath = _modelPathPin.GetValue<string>() ?? GetPropertyValue<string>("modelPath");
        return !string.IsNullOrWhiteSpace(modelPath);
    }
}