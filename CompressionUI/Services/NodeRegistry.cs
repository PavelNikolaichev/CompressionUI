using CompressionUI.Models.Nodes;
using CompressionUI.Models.Nodes.Data;
using CompressionUI.Models.Nodes.Math;
using CompressionUI.Models.Nodes.Model;
using CompressionUI.Models.Nodes.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CompressionUI.Services;

/// <summary>
/// Service for registering and creating nodes
/// </summary>
public class NodeRegistry : INodeRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NodeRegistry> _logger;
    private readonly ConcurrentDictionary<string, NodeTypeInfo> _registeredNodes = new();

    public NodeRegistry(IServiceProvider serviceProvider, ILogger<NodeRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Register built-in nodes
        RegisterBuiltInNodes();
    }

    private void RegisterBuiltInNodes()
    {
        _logger.LogInformation("Registering built-in nodes...");

        // Utility nodes
        RegisterNode<DebugPrintNode>("Debug Print");
        RegisterNode<VariableNode>("Variable");
        RegisterNode<MemoryCleanupNode>("Memory Cleanup");

        // Data nodes
        RegisterNode<TextDataLoaderNode>("Text Data Loader");
        RegisterNode<ImageDataLoaderNode>("Image Data Loader");

        // Math nodes
        RegisterNode<ArithmeticNode>("Arithmetic");

        // Model nodes
        RegisterNode<PyTorchModelNode>("PyTorch Model");

        _logger.LogInformation("Registered {Count} built-in nodes", _registeredNodes.Count);
    }

    public void RegisterNode<T>(string? customName = null) where T : INode
    {
        RegisterNode(typeof(T), customName);
    }

    public void RegisterNode(Type nodeType, string? customName = null)
    {
        if (!typeof(INode).IsAssignableFrom(nodeType))
        {
            throw new ArgumentException($"Type {nodeType.Name} does not implement INode", nameof(nodeType));
        }

        try
        {
            // Create a temporary instance to get metadata
            var tempInstance = CreateNodeInstance(nodeType);
            if (tempInstance == null)
            {
                _logger.LogWarning("Failed to create temporary instance of {NodeType}", nodeType.Name);
                return;
            }

            var typeInfo = new NodeTypeInfo
            {
                TypeName = nodeType.Name,
                DisplayName = customName ?? tempInstance.Name,
                Description = tempInstance.Description,
                Category = tempInstance.Category,
                Version = tempInstance.Version,
                NodeType = nodeType,
                Tags = ExtractTagsFromType(nodeType),
                IsExperimental = HasExperimentalAttribute(nodeType)
            };

            _registeredNodes.AddOrUpdate(nodeType.Name, typeInfo, (_, _) => typeInfo);
            
            _logger.LogDebug("Registered node: {DisplayName} ({TypeName})", typeInfo.DisplayName, typeInfo.TypeName);
            
            // Dispose temporary instance if it implements IDisposable
            tempInstance.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register node type: {NodeType}", nodeType.Name);
        }
    }

    public void RegisterAssemblyNodes(Assembly assembly)
    {
        _logger.LogInformation("Scanning assembly {AssemblyName} for nodes...", assembly.GetName().Name);

        var nodeTypes = assembly.GetTypes()
            .Where(t => typeof(INode).IsAssignableFrom(t) && 
                       !t.IsAbstract && 
                       !t.IsInterface)
            .ToList();

        foreach (var nodeType in nodeTypes)
        {
            try
            {
                RegisterNode(nodeType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register node type {NodeType} from assembly", nodeType.Name);
            }
        }

        _logger.LogInformation("Registered {Count} nodes from assembly {AssemblyName}", 
            nodeTypes.Count, assembly.GetName().Name);
    }

    public INode CreateNode(string nodeTypeName)
    {
        var node = TryCreateNode(nodeTypeName);
        if (node == null)
        {
            throw new InvalidOperationException($"Node type '{nodeTypeName}' is not registered");
        }
        return node;
    }

    public T CreateNode<T>() where T : INode
    {
        var node = TryCreateNode(typeof(T).Name);
        if (node is T typedNode)
        {
            return typedNode;
        }
        
        throw new InvalidOperationException($"Failed to create node of type {typeof(T).Name}");
    }

    public INode? TryCreateNode(string nodeTypeName)
    {
        if (!_registeredNodes.TryGetValue(nodeTypeName, out var typeInfo))
        {
            _logger.LogWarning("Attempted to create unregistered node type: {NodeTypeName}", nodeTypeName);
            return null;
        }

        try
        {
            var node = CreateNodeInstance(typeInfo.NodeType);
            if (node != null)
            {
                _logger.LogDebug("Created node instance: {NodeTypeName} (ID: {NodeId})", nodeTypeName, node.Id);
            }
            return node;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create node instance: {NodeTypeName}", nodeTypeName);
            return null;
        }
    }

    private INode? CreateNodeInstance(Type nodeType)
    {
        try
        {
            // Try to create using dependency injection first
            if (_serviceProvider.GetService(nodeType) is INode serviceNode)
            {
                return serviceNode;
            }

            // Try constructor with common dependencies
            var constructors = nodeType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length);

            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                var args = new object[parameters.Length];
                var canCreate = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    var service = _serviceProvider.GetService(paramType);
                    
                    if (service != null)
                    {
                        args[i] = service;
                    }
                    else if (parameters[i].HasDefaultValue)
                    {
                        args[i] = parameters[i].DefaultValue!;
                    }
                    else
                    {
                        canCreate = false;
                        break;
                    }
                }

                if (canCreate)
                {
                    return (INode)Activator.CreateInstance(nodeType, args)!;
                }
            }

            // Last resort: parameterless constructor
            return (INode)Activator.CreateInstance(nodeType)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create instance of {NodeType}", nodeType.Name);
            return null;
        }
    }

    public IEnumerable<NodeTypeInfo> GetAllNodeTypes()
    {
        return _registeredNodes.Values.OrderBy(n => n.Category).ThenBy(n => n.DisplayName);
    }

    public IEnumerable<NodeTypeInfo> GetNodeTypesByCategory(string category)
    {
        return _registeredNodes.Values
            .Where(n => n.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.DisplayName);
    }

    public IEnumerable<string> GetCategories()
    {
        return _registeredNodes.Values
            .Select(n => n.Category)
            .Distinct()
            .OrderBy(c => c);
    }

    public NodeTypeInfo? GetNodeTypeInfo(string nodeTypeName)
    {
        _registeredNodes.TryGetValue(nodeTypeName, out var typeInfo);
        return typeInfo;
    }

    public bool IsRegistered(string nodeTypeName)
    {
        return _registeredNodes.ContainsKey(nodeTypeName);
    }

    public bool IsRegistered<T>() where T : INode
    {
        return _registeredNodes.ContainsKey(typeof(T).Name);
    }

    private static string[] ExtractTagsFromType(Type nodeType)
    {
        var tags = new List<string>();
        
        // Add category as tag
        if (CreateTempInstance(nodeType) is INode tempNode)
        {
            tags.Add(tempNode.Category.ToLower());
            tempNode.Dispose();
        }

        // Add tags from attributes if any
        var attributes = nodeType.GetCustomAttributes();
        foreach (var attr in attributes)
        {
            if (attr is TagAttribute tagAttr)
            {
                tags.AddRange(tagAttr.Tags);
            }
        }

        return tags.Distinct().ToArray();
    }

    private static bool HasExperimentalAttribute(Type nodeType)
    {
        return nodeType.GetCustomAttribute<ExperimentalAttribute>() != null;
    }

    private static INode? CreateTempInstance(Type nodeType)
    {
        try
        {
            return (INode?)Activator.CreateInstance(nodeType);
        }
        catch
        {
            return null;
        }
    }
}

// Helper attributes for node metadata
[AttributeUsage(AttributeTargets.Class)]
public class TagAttribute : Attribute
{
    public string[] Tags { get; }
    
    public TagAttribute(params string[] tags)
    {
        Tags = tags;
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class ExperimentalAttribute : Attribute
{
}