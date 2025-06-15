using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes.Data;

/// <summary>
/// Manages datasets for machine learning workflows
/// </summary>
public class DatasetNode : NodeBase
{
    public override string Category => NodeCategories.Data;

    private NodePin _datasetPathPin = null!;
    private NodePin _datasetOutputPin = null!;
    private NodePin _sizeOutputPin = null!;
    private NodePin _infoOutputPin = null!;
    
    private NodeProperty _datasetPathProperty = null!;
    private NodeProperty _datasetTypeProperty = null!;
    private NodeProperty _splitRatioProperty = null!;
    private NodeProperty _shuffleProperty = null!;

    public DatasetNode(ILogger<DatasetNode>? logger = null) : base(logger)
    {
        Name = "Dataset";
        Description = "Manages datasets for machine learning workflows";
        InitializePinsAndProperties();
    }

    protected override void InitializePinsAndProperties()
    {
        // Input
        _datasetPathPin = AddInputPin("datasetPath", "Dataset Path", DataTypes.FilePath, isRequired: false);
        
        // Outputs
        _datasetOutputPin = AddOutputPin("dataset", "Dataset", DataTypes.Any);
        _sizeOutputPin = AddOutputPin("size", "Dataset Size", DataTypes.Integer);
        _infoOutputPin = AddOutputPin("info", "Dataset Info", DataTypes.String);
        
        // Properties
        _datasetPathProperty = AddProperty("datasetPath", "Dataset Path", PropertyType.DirectoryPath, "");
        
        _datasetTypeProperty = AddProperty("datasetType", "Dataset Type", PropertyType.Enum, "ImageFolder");
        _datasetTypeProperty.EnumValues = new[] { "ImageFolder", "TextFiles", "CSV", "Custom" };
        
        _splitRatioProperty = AddProperty("splitRatio", "Train/Val Split", PropertyType.String, "0.8/0.2");
        _shuffleProperty = AddProperty("shuffle", "Shuffle Data", PropertyType.Boolean, true);
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context)
    {
        var datasetPath = _datasetPathPin.GetValue<string>() ?? GetPropertyValue<string>("datasetPath");
        var datasetType = GetPropertyValue<string>("datasetType") ?? "ImageFolder";
        var shuffle = GetPropertyValue<bool>("shuffle");

        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            return NodeExecutionResult.Failed("No dataset path specified");
        }

        if (!Directory.Exists(datasetPath))
        {
            return NodeExecutionResult.Failed($"Dataset directory not found: {datasetPath}");
        }

        try
        {
            context.ReportProgress($"Loading {datasetType} dataset from: {datasetPath}");

            var datasetInfo = await AnalyzeDatasetAsync(datasetPath, datasetType);
            
            _datasetOutputPin.Value = $"Dataset({datasetPath})"; // Placeholder for actual dataset object
            _sizeOutputPin.Value = datasetInfo.Count;
            _infoOutputPin.Value = datasetInfo.ToString();

            context.ReportProgress($"Dataset loaded: {datasetInfo.Count} items");
            _logger?.LogInformation("Loaded dataset: {DatasetPath} ({Count} items)", datasetPath, datasetInfo.Count);

            return NodeExecutionResult.Successful(TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load dataset: {DatasetPath}", datasetPath);
            return NodeExecutionResult.Failed($"Failed to load dataset: {ex.Message}");
        }
    }

    private async Task<DatasetInfo> AnalyzeDatasetAsync(string path, string type)
    {
        return await Task.Run(() =>
        {
            return type switch
            {
                "ImageFolder" => AnalyzeImageFolder(path),
                "TextFiles" => AnalyzeTextFiles(path),
                "CSV" => AnalyzeCsvFiles(path),
                _ => new DatasetInfo { Path = path, Type = type, Count = 0 }
            };
        });
    }

    private DatasetInfo AnalyzeImageFolder(string path)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };
        var imageFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        var folders = Directory.GetDirectories(path).Select(Path.GetFileName).ToList();

        return new DatasetInfo
        {
            Path = path,
            Type = "ImageFolder",
            Count = imageFiles.Count,
            Details = $"Classes: {folders.Count}, Images: {imageFiles.Count}"
        };
    }

    private DatasetInfo AnalyzeTextFiles(string path)
    {
        var textFiles = Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories);
        
        return new DatasetInfo
        {
            Path = path,
            Type = "TextFiles", 
            Count = textFiles.Length,
            Details = $"Text files: {textFiles.Length}"
        };
    }

    private DatasetInfo AnalyzeCsvFiles(string path)
    {
        var csvFiles = Directory.GetFiles(path, "*.csv", SearchOption.AllDirectories);
        
        return new DatasetInfo
        {
            Path = path,
            Type = "CSV",
            Count = csvFiles.Length,
            Details = $"CSV files: {csvFiles.Length}"
        };
    }

    private class DatasetInfo
    {
        public string Path { get; set; } = "";
        public string Type { get; set; } = "";
        public int Count { get; set; }
        public string Details { get; set; } = "";

        public override string ToString() => $"{Type} Dataset: {Details}";
    }

    public override bool CanExecute()
    {
        var datasetPath = _datasetPathPin.GetValue<string>() ?? GetPropertyValue<string>("datasetPath");
        return !string.IsNullOrWhiteSpace(datasetPath);
    }
}