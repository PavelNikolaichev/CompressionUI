using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CompressionUI.Models.Nodes;

/// <summary>
/// Provides context and services for node execution
/// </summary>
public class NodeExecutionContext
{
    public CancellationToken CancellationToken { get; }
    public IProgress<string>? Progress { get; }
    public Dictionary<string, object> SharedData { get; }
    public DateTime StartTime { get; }

    public NodeExecutionContext(
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        CancellationToken = cancellationToken;
        Progress = progress;
        SharedData = new Dictionary<string, object>();
        StartTime = DateTime.UtcNow;
    }

    public void ReportProgress(string message)
    {
        Progress?.Report(message);
    }

    public void ThrowIfCancellationRequested()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }
}

/// <summary>
/// Result of node execution
/// </summary>
public class NodeExecutionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public Dictionary<string, object> OutputData { get; set; } = new();

    public static NodeExecutionResult Successful(TimeSpan executionTime) =>
        new() { Success = true, ExecutionTime = executionTime };

    public static NodeExecutionResult Failed(string error, Exception? exception = null) =>
        new() { Success = false, ErrorMessage = error, Exception = exception };
}