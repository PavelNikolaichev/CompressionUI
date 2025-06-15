using CompressionUI.Models.Nodes;
using CompressionUI.Services.Execution;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompressionUI.Services;

/// <summary>
/// Main service for executing node graphs
/// </summary>
public class NodeExecutionService
{
    private readonly ILogger<NodeExecutionService> _logger;
    private readonly Dictionary<string, INodeExecutionStrategy> _strategies = new();
    private string _currentStrategy = "Sequential";

    public NodeExecutionService(
        IEnumerable<INodeExecutionStrategy> strategies,
        ILogger<NodeExecutionService> logger)
    {
        _logger = logger;
        
        // Register all available strategies
        foreach (var strategy in strategies)
        {
            _strategies[strategy.Name] = strategy;
            _logger.LogDebug("Registered execution strategy: {StrategyName}", strategy.Name);
        }

        if (!_strategies.Any())
        {
            _logger.LogWarning("No execution strategies registered");
        }
    }

    /// <summary>
    /// Available execution strategies
    /// </summary>
    public IEnumerable<string> AvailableStrategies => _strategies.Keys;

    /// <summary>
    /// Current execution strategy
    /// </summary>
    public string CurrentStrategy
    {
        get => _currentStrategy;
        set
        {
            if (_strategies.ContainsKey(value))
            {
                _currentStrategy = value;
                _logger.LogDebug("Switched to execution strategy: {StrategyName}", value);
            }
            else
            {
                throw new ArgumentException($"Unknown execution strategy: {value}");
            }
        }
    }

    /// <summary>
    /// Execute a collection of nodes using the current strategy
    /// </summary>
    public async Task<NodeGraphExecutionResult> ExecuteNodesAsync(
        IEnumerable<INode> nodes,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        var context = new NodeExecutionContext(cancellationToken, progress);
        return await ExecuteNodesAsync(nodes, context);
    }

    /// <summary>
    /// Execute a collection of nodes with a custom execution context
    /// </summary>
    public async Task<NodeGraphExecutionResult> ExecuteNodesAsync(
        IEnumerable<INode> nodes,
        NodeExecutionContext context)
    {
        var nodeList = nodes.ToList();
        
        if (!nodeList.Any())
        {
            _logger.LogWarning("No nodes provided for execution");
            return NodeGraphExecutionResult.Failed("No nodes provided for execution");
        }

        if (!_strategies.TryGetValue(_currentStrategy, out var strategy))
        {
            var error = $"Execution strategy '{_currentStrategy}' not found";
            _logger.LogError(error);
            return NodeGraphExecutionResult.Failed(error);
        }

        try
        {
            _logger.LogInformation("Starting node graph execution with {NodeCount} nodes using {Strategy} strategy", 
                nodeList.Count, _currentStrategy);

            var result = await strategy.ExecuteAsync(nodeList, context);
            
            _logger.LogInformation("Node graph execution completed: Success={Success}, " +
                "NodesExecuted={NodesExecuted}, NodesFailed={NodesFailed}, Time={TotalTime}",
                result.Success, result.NodesExecuted, result.NodesFailed, result.TotalExecutionTime);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Node graph execution failed with unexpected error");
            return NodeGraphExecutionResult.Failed($"Execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Execute a single node
    /// </summary>
    public async Task<NodeExecutionResult> ExecuteNodeAsync(
        INode node,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        var context = new NodeExecutionContext(cancellationToken, progress);
        
        _logger.LogDebug("Executing single node: {NodeName} ({NodeId})", node.Name, node.Id);
        
        try
        {
            var result = await node.ExecuteAsync(context);
            
            _logger.LogDebug("Single node execution completed: {NodeName}, Success={Success}", 
                node.Name, result.Success);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Single node execution failed: {NodeName}", node.Name);
            return NodeExecutionResult.Failed($"Node execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validate that a collection of nodes can be executed
    /// </summary>
    public List<string> ValidateNodeGraph(IEnumerable<INode> nodes)
    {
        var errors = new List<string>();
        var nodeList = nodes.ToList();

        if (!nodeList.Any())
        {
            errors.Add("No nodes provided");
            return errors;
        }

        try
        {
            // Use dependency resolver to validate
            var dependencyResolver = new NodeDependencyResolver(
                Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole())
                .CreateLogger<NodeDependencyResolver>());
            
            var dependencyErrors = dependencyResolver.ValidateDependencies(nodeList);
            errors.AddRange(dependencyErrors);

            // Try to resolve execution order to check for cycles
            try
            {
                dependencyResolver.ResolveExecutionOrder(nodeList);
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"Dependency resolution failed: {ex.Message}");
            }

            // Validate individual nodes
            foreach (var node in nodeList)
            {
                var nodeErrors = node.ValidateConfiguration();
                foreach (var error in nodeErrors)
                {
                    errors.Add($"Node '{node.Name}': {error}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Validation failed: {ex.Message}");
        }

        return errors;
    }

    /// <summary>
    /// Get execution statistics for the last run
    /// </summary>
    public NodeExecutionStatistics GetExecutionStatistics(NodeGraphExecutionResult result)
    {
        return new NodeExecutionStatistics
        {
            TotalNodes = result.NodesExecuted + result.NodesFailed + result.NodesSkipped,
            ExecutedNodes = result.NodesExecuted,
            FailedNodes = result.NodesFailed,
            SkippedNodes = result.NodesSkipped,
            TotalExecutionTime = result.TotalExecutionTime,
            AverageNodeExecutionTime = result.NodesExecuted > 0 
                ? TimeSpan.FromMilliseconds(result.TotalExecutionTime.TotalMilliseconds / result.NodesExecuted)
                : TimeSpan.Zero,
            ErrorCount = result.Errors.Count,
            SuccessRate = result.NodesExecuted + result.NodesFailed + result.NodesSkipped > 0
                ? (double)result.NodesExecuted / (result.NodesExecuted + result.NodesFailed + result.NodesSkipped) * 100
                : 0
        };
    }
}

/// <summary>
/// Execution statistics
/// </summary>
public class NodeExecutionStatistics
{
    public int TotalNodes { get; set; }
    public int ExecutedNodes { get; set; }
    public int FailedNodes { get; set; }
    public int SkippedNodes { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan AverageNodeExecutionTime { get; set; }
    public int ErrorCount { get; set; }
    public double SuccessRate { get; set; }

    public override string ToString() =>
        $"Executed: {ExecutedNodes}/{TotalNodes} ({SuccessRate:F1}%), " +
        $"Time: {TotalExecutionTime:mm\\:ss\\.ff}, " +
        $"Avg: {AverageNodeExecutionTime:ss\\.ff}s/node";
}