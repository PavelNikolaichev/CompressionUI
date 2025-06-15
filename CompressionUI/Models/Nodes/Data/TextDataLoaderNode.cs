using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes.Data;

/// <summary>
/// Loads text data from files - essential for NLP and text processing workflows
/// </summary>
public class TextDataLoaderNode : NodeBase
{
    public override string Category => NodeCategories.Data;

    private NodePin _filePathPin = null!;
    private NodePin _textOutputPin = null!;
    private NodePin _lengthOutputPin = null!;
    private NodeProperty _filePathProperty = null!;
    private NodeProperty _encodingProperty = null!;

    public TextDataLoaderNode(ILogger<TextDataLoaderNode>? logger = null) : base(logger)
    {
        Name = "Text Data Loader";
        Description = "Loads text content from files";
        InitializePinsAndProperties();
    }

    protected override void InitializePinsAndProperties()
    {
        // Input: optional file path (can also use property)
        _filePathPin = AddInputPin("filePath", "File Path", DataTypes.FilePath, isRequired: false);
        
        // Outputs
        _textOutputPin = AddOutputPin("text", "Text Content", DataTypes.String);
        _lengthOutputPin = AddOutputPin("length", "Character Count", DataTypes.Integer);
        
        // Properties
        _filePathProperty = AddProperty("filePath", "File Path", PropertyType.FilePath, "");
        _filePathProperty.FileFilter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
        
        _encodingProperty = AddProperty("encoding", "Text Encoding", PropertyType.Enum, "UTF-8");
        _encodingProperty.EnumValues = new[] { "UTF-8", "ASCII", "UTF-16", "UTF-32" };
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context)
    {
        // Get file path from input pin or property
        var filePath = _filePathPin.GetValue<string>() ?? GetPropertyValue<string>("filePath");
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NodeExecutionResult.Failed("No file path specified");
        }

        if (!File.Exists(filePath))
        {
            return NodeExecutionResult.Failed($"File not found: {filePath}");
        }

        try
        {
            context.ReportProgress($"Loading text from: {Path.GetFileName(filePath)}");

            var encoding = GetPropertyValue<string>("encoding") switch
            {
                "ASCII" => System.Text.Encoding.ASCII,
                "UTF-16" => System.Text.Encoding.Unicode,
                "UTF-32" => System.Text.Encoding.UTF32,
                _ => System.Text.Encoding.UTF8
            };

            var text = await File.ReadAllTextAsync(filePath, encoding);
            
            _textOutputPin.Value = text;
            _lengthOutputPin.Value = text.Length;

            context.ReportProgress($"Loaded {text.Length:N0} characters");
            _logger?.LogInformation("Loaded text file: {FilePath} ({Length} characters)", filePath, text.Length);

            return NodeExecutionResult.Successful(TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load text file: {FilePath}", filePath);
            return NodeExecutionResult.Failed($"Failed to load file: {ex.Message}");
        }
    }

    public override bool CanExecute()
    {
        var filePath = _filePathPin.GetValue<string>() ?? GetPropertyValue<string>("filePath");
        return !string.IsNullOrWhiteSpace(filePath);
    }
}