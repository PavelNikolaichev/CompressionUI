using CompressionUI.Models.Nodes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompressionUI.Services.Execution;

/// <summary>
/// Executes nodes sequentially in dependency order
/// </summary>
public class SequentialExecutionStrategy : INodeExecutionStrategy
{
    private readonly NodeDependencyResolver _dependencyResolver;
    private readonly ILogger<SequentialExecutionStrategy> _logger;

    public string Name => "Sequential";
    public string Description => "Executes nodes one by one in dependency order";

    public SequentialExecutionStrategy(
        NodeDependencyResolver dependencyResolver,
        ILogger<SequentialExecutionStrategy> logger)
    {
        _dependencyResolver = dependencyResolver;
        _logger = logger;
    }

    public async Task<NodeGraphExecutionResult> ExecuteAsync(
        IEnumerable<INode> nodes, 
        NodeExecutionContext context)
    {
        var nodeList = nodes.ToList();
        var stopwatch = Stopwatch.StartNew();
        var result = new NodeGraphExecutionResult();

        try
        {
            _logger.LogInformation("Starting sequential execution of {NodeCount} nodes", nodeList.Count);
            context.ReportProgress($"Starting execution of {nodeList.Count} nodes...");

            // Validate dependencies
            var validationErrors = _dependencyResolver.ValidateDependencies(nodeList);
            if (validationErrors.Any())
            {
                foreach (var error in validationErrors)
                {
                    result.Errors.Add(new NodeExecutionError(error));
                    _logger.LogError("Validation error: {Error}", error);
                }
                return result;
            }

            // Resolve execution order
            List<INode> executionOrder;
            try
            {
                executionOrder = _dependencyResolver.ResolveExecutionOrder(nodeList);
            }
            catch (InvalidOperationException ex)
            {
                result.Errors.Add(new NodeExecutionError($"Failed to resolve execution order: {ex.Message}"));
                return result;
            }

            // Reset all nodes before execution
            foreach (var node in nodeList)
            {
                node.Reset();
            }

            // Execute nodes in order
            var nodeIndex = 0;
            foreach (var node in executionOrder)
            {
                nodeIndex++;
                context.ThrowIfCancellationRequested();

                try
                {
                    _logger.LogDebug("Executing node {NodeIndex}/{TotalNodes}: {NodeName} ({NodeId})", 
                        nodeIndex, executionOrder.Count, node.Name, node.Id);
                    
                    context.ReportProgress($"[{nodeIndex}/{executionOrder.Count}] Executing {node.Name}...");

                    // Check if node can execute
                    if (!node.CanExecute())
                    {
                        var configErrors = node.ValidateConfiguration().ToList();
                        if (configErrors.Any())
                        {
                            var errorMessage = $"Node validation failed: {string.Join(", ", configErrors)}";
                            result.Errors.Add(new NodeExecutionError(node, errorMessage));
                            result.NodesFailed++;
                            _logger.LogWarning("Skipping node {NodeName}: {ValidationErrors}", 
                                node.Name, string.Join(", ", configErrors));
                            continue;
                        }
                    }

                    // Execute the node
                    var nodeResult = await node.ExecuteAsync(context);
                    
                    if (nodeResult.Success)
                    {
                        result.NodesExecuted++;
                        _logger.LogDebug("Node {NodeName} completed successfully in {ExecutionTime}", 
                            node.Name, nodeResult.ExecutionTime);
                    }
                    else
                    {
                        result.NodesFailed++;
                        result.Errors.Add(new NodeExecutionError(node, 
                            nodeResult.ErrorMessage ?? "Node execution failed", 
                            nodeResult.Exception));
                        
                        _logger.LogError("Node {NodeName} failed: {ErrorMessage}", 
                            node.Name, nodeResult.ErrorMessage);
                        
                        // Stop execution on first failure (can be made configurable)
                        context.ReportProgress($"Execution stopped due to failure in {node.Name}");
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Node execution cancelled at node {NodeName}", node.Name);
                    context.ReportProgress("Execution cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    result.NodesFailed++;
                    result.Errors.Add(new NodeExecutionError(node, ex.Message, ex));
                    _logger.LogError(ex, "Unexpected error executing node {NodeName}", node.Name);
                    
                    // Stop on unexpected errors
                    context.ReportProgress($"Execution stopped due to unexpected error in {node.Name}");
                    break;
                }
            }

            // Determine overall success
            result.Success = result.NodesFailed == 0 && result.NodesExecuted > 0;
            result.TotalExecutionTime = stopwatch.Elapsed;

            if (result.Success)
            {
                _logger.LogInformation("Sequential execution completed successfully. " +
                    "Executed {NodesExecuted} nodes in {TotalTime}", 
                    result.NodesExecuted, result.TotalExecutionTime);
                context.ReportProgress($"Execution completed: {result.NodesExecuted} nodes in {result.TotalExecutionTime:mm\\:ss\\.ff}");
            }
            else
            {
                _logger.LogWarning("Sequential execution completed with errors. " +
                    "Executed: {NodesExecuted}, Failed: {NodesFailed}, Errors: {ErrorCount}",
                    result.NodesExecuted, result.NodesFailed, result.Errors.Count);
                context.ReportProgress($"Execution completed with {result.NodesFailed} failures");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.TotalExecutionTime = stopwatch.Elapsed;
            result.Errors.Add(new NodeExecutionError("Execution was cancelled"));
            return result;
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}