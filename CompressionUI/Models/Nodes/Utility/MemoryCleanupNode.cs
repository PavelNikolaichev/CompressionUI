using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes.Utility;

/// <summary>
/// Forces garbage collection - useful for memory management in ML workflows
/// </summary>
public class MemoryCleanupNode : NodeBase
{
    public override string Category => NodeCategories.Utility;

    private NodePin _triggerPin = null!;
    private NodePin _outputPin = null!;
    private NodeProperty _forceFullCollectionProperty = null!;

    public MemoryCleanupNode(ILogger<MemoryCleanupNode>? logger = null) : base(logger)
    {
        Name = "Memory Cleanup";
        Description = "Forces garbage collection to free up memory";
        InitializePinsAndProperties();
    }

    protected override void InitializePinsAndProperties()
    {
        _triggerPin = AddInputPin("trigger", "Trigger", DataTypes.Any, isRequired: false);
        _outputPin = AddOutputPin("output", "Memory Info", DataTypes.String);
        
        _forceFullCollectionProperty = AddProperty("forceFullCollection", "Force Full Collection", PropertyType.Boolean, true);
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(NodeExecutionContext context)
    {
        var memoryBefore = GC.GetTotalMemory(false);
        var forceFullCollection = GetPropertyValue<bool>("forceFullCollection");

        context.ReportProgress("Running garbage collection...");

        if (forceFullCollection)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        else
        {
            GC.Collect();
        }

        var memoryAfter = GC.GetTotalMemory(false);
        var freedBytes = memoryBefore - memoryAfter;
        
        var memoryInfo = $"Memory before: {memoryBefore:N0} bytes\n" +
                        $"Memory after: {memoryAfter:N0} bytes\n" +
                        $"Freed: {freedBytes:N0} bytes";

        _outputPin.Value = memoryInfo;
        
        _logger?.LogInformation("Memory cleanup completed. Freed {FreedBytes} bytes", freedBytes);
        context.ReportProgress($"Memory cleanup freed {freedBytes:N0} bytes");

        return await Task.FromResult(NodeExecutionResult.Successful(TimeSpan.Zero));
    }
}