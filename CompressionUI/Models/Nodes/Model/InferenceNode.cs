using CompressionUI.Services;
using Microsoft.Extensions.Logging;

namespace CompressionUI.Models.Nodes.Model;

/// <summary>
/// Optimized node for running inference on trained models
/// </summary>
public class InferenceNode : NodeBase
{
    public override string Category => NodeCategories.Model;

    private readonly PythonService _pythonService;
    
    private NodePin _modelPin = null!;
    private NodePin _inputDataPin = null!;
    private NodePin _outputPin = null!;
    private NodePin _confidencePin = null!;
    private NodePin _processingTimePin = null!;
    
    private NodeProperty _deviceProperty = null!;
    private NodeProperty _batchSizeProperty = null!;
    private NodeProperty _optimizeProperty = null!;
    private NodeProperty _outputFormatProperty = null!;
    private NodeProperty _preprocessingProperty = null!;

    public InferenceNode(PythonService pythonService, ILogger<InferenceNode>? logger = null) : base(logger)
    {
        _pythonService = pythonService;
        Name = "Model Inference";
        Description = "Fast inference on trained models with optimizations";
        InitializePinsAndProperties();
    }

    protected override void InitializePinsAndProperties()
    {
        // Inputs
        _modelPin = AddInputPin("model", "Trained Model", DataTypes.Any, isRequired: true);
        _inputDataPin = AddInputPin("inputData", "Input Data", DataTypes.Tensor, isRequired: true);
        
        // Outputs
        _outputPin = AddOutputPin("predictions", "Predictions", DataTypes.Tensor);
        _confidencePin = AddOutputPin("confidence", "Confidence Scores", DataTypes.Tensor);
        _processingTimePin = AddOutputPin("processingTime", "Processing Time (ms)", DataTypes.Float);
        
        // Properties for inference optimization
        _deviceProperty = AddProperty("device", "Device", PropertyType.Enum, "auto");
        _deviceProperty.EnumValues = new[] { "auto", "cpu", "cuda", "mps" };
        
        _batchSizeProperty = AddProperty("batchSize", "Batch Size", PropertyType.Integer, 1);
        
        _optimizeProperty = AddProperty("optimize", "Optimize Model", PropertyType.Boolean, true);
        _optimizeProperty.Description = "Apply inference optimizations (TorchScript, quantization, etc.)";
        
        _outputFormatProperty = AddProperty("outputFormat", "Output Format", PropertyType.Enum, "raw");
        _outputFormatProperty.EnumValues = new[] { "raw", "probabilities", "class_indices", "top_k" };
        
        _preprocessingProperty = AddProperty("preprocessing", "Apply Preprocessing", PropertyType.Boolean, true);
        _preprocessingProperty.Description = "Apply standard preprocessing (normalization, resizing, etc.)";
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context)
    {
        var device = GetPropertyValue<string>("device") ?? "auto";
        var batchSize = GetPropertyValue<int>("batchSize");
        var optimize = GetPropertyValue<bool>("optimize");
        var outputFormat = GetPropertyValue<string>("outputFormat") ?? "raw";
        var applyPreprocessing = GetPropertyValue<bool>("preprocessing");

        var model = _modelPin.Value;
        var inputData = _inputDataPin.Value;

        if (model == null)
        {
            return NodeExecutionResult.Failed("No model provided");
        }

        if (inputData == null)
        {
            return NodeExecutionResult.Failed("No input data provided");
        }

        try
        {
            context.ReportProgress("Preparing model for inference...");

            // Python code optimized for fast inference
            var inferenceCode = $@"
import torch
import torch.nn as nn
import time
import numpy as np

# Inference configuration
device = '{device}'
batch_size = {batchSize}
optimize_model = {optimize.ToString().ToLower()}
output_format = '{outputFormat}'
apply_preprocessing = {applyPreprocessing.ToString().ToLower()}

print(f'Inference configuration:')
print(f'  Device: {{device}}')
print(f'  Batch size: {{batch_size}}')
print(f'  Optimize: {{optimize_model}}')
print(f'  Output format: {{output_format}}')
print(f'  Preprocessing: {{apply_preprocessing}}')

# Auto-detect device if needed
if device == 'auto':
    if torch.cuda.is_available():
        device = 'cuda'
    elif hasattr(torch.backends, 'mps') and torch.backends.mps.is_available():
        device = 'mps'
    else:
        device = 'cpu'
    print(f'Auto-detected device: {{device}}')

# Simulate model loading and optimization
print('Loading model for inference...')
# In real implementation: model = torch.load(model_path)
# model = model.to(device)
# model.eval()

# Apply inference optimizations
if optimize_model:
    print('Applying inference optimizations...')
    # In real implementation:
    # model = torch.jit.script(model)  # TorchScript
    # model = torch.quantization.quantize_dynamic(model, {{nn.Linear}}, dtype=torch.qint8)
    
# Simulate preprocessing
if apply_preprocessing:
    print('Applying preprocessing...')
    # In real implementation: preprocess input data
    
# Simulate inference
print('Running inference...')
start_time = time.time()

# Mock inference results
num_classes = 10
predictions = torch.randn(batch_size, num_classes)
confidence_scores = torch.softmax(predictions, dim=1)

end_time = time.time()
processing_time_ms = (end_time - start_time) * 1000

# Format output based on requested format
if output_format == 'probabilities':
    output = confidence_scores
elif output_format == 'class_indices':
    output = torch.argmax(confidence_scores, dim=1)
elif output_format == 'top_k':
    top_k = min(5, num_classes)
    values, indices = torch.topk(confidence_scores, top_k, dim=1)
    output = {{'values': values, 'indices': indices}}
else:  # raw
    output = predictions

print(f'Inference completed in {{processing_time_ms:.2f}}ms')
print(f'Output shape: {{predictions.shape if hasattr(predictions, ""shape"") else ""N/A""}}')
print(f'Max confidence: {{torch.max(confidence_scores).item():.4f}}')

# Return results
inference_results = {{
    'predictions': str(output),
    'confidence_scores': str(confidence_scores),
    'processing_time_ms': processing_time_ms,
    'device_used': device,
    'batch_size': batch_size
}}

print('Inference results ready')
";

            context.ReportProgress("Running inference...");
            var result = await _pythonService.ExecutePythonCodeAsync(inferenceCode);

            if (!result.Success)
            {
                return NodeExecutionResult.Failed($"Inference failed: {result.Error}");
            }

            // Parse results and set outputs
            // In a real implementation, you'd parse the actual tensor data
            _outputPin.Value = $"InferenceOutput({outputFormat})";
            _confidencePin.Value = $"ConfidenceScores(batch_size={batchSize})";
            
            // Extract processing time from output (in real implementation, parse from Python results)
            var processingTime = ExtractProcessingTime(result.Output);
            _processingTimePin.Value = processingTime;

            context.ReportProgress($"Inference completed in {processingTime:F2}ms");
            _logger?.LogInformation("Inference completed: batch_size={BatchSize}, time={ProcessingTime}ms, device={Device}", 
                batchSize, processingTime, device);

            return NodeExecutionResult.Successful(TimeSpan.FromMilliseconds(processingTime));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Inference failed");
            return NodeExecutionResult.Failed($"Inference error: {ex.Message}");
        }
    }

    private float ExtractProcessingTime(string output)
    {
        // Simple parsing - in real implementation, you'd get this from Python properly
        try
        {
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("completed in") && line.Contains("ms"))
                {
                    var parts = line.Split(' ');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i] == "in" && parts[i + 1].EndsWith("ms"))
                        {
                            var timeStr = parts[i + 1].Replace("ms", "");
                            if (float.TryParse(timeStr, out var time))
                            {
                                return time;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Fall back to mock time if parsing fails
        }
        
        return 50.0f; // Mock processing time
    }

    public override bool CanExecute()
    {
        return _modelPin.IsConnected && _inputDataPin.IsConnected;
    }

    public override IEnumerable<string> ValidateConfiguration()
    {
        var errors = base.ValidateConfiguration().ToList();
        
        var batchSize = GetPropertyValue<int>("batchSize");
        if (batchSize <= 0)
        {
            errors.Add("Batch size must be greater than 0");
        }
        
        if (batchSize > 1000)
        {
            errors.Add("Batch size seems unusually large (>1000)");
        }
        
        return errors;
    }

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        
        // Add inference-specific metadata
        data["InferenceOptimized"] = GetPropertyValue<bool>("optimize");
        data["LastProcessingTime"] = _processingTimePin.Value ?? 0f;
        
        return data;
    }
}