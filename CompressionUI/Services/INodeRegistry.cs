using CompressionUI.Models.Nodes;
using System;
using System.Collections.Generic;

namespace CompressionUI.Services;

/// <summary>
/// Interface for node registration and discovery
/// </summary>
public interface INodeRegistry
{
    // Registration
    void RegisterNode<T>(string? customName = null) where T : INode;
    void RegisterNode(Type nodeType, string? customName = null);
    void RegisterAssemblyNodes(System.Reflection.Assembly assembly);
    
    // Node creation
    INode CreateNode(string nodeTypeName);
    T CreateNode<T>() where T : INode;
    INode? TryCreateNode(string nodeTypeName);
    
    // Discovery
    IEnumerable<NodeTypeInfo> GetAllNodeTypes();
    IEnumerable<NodeTypeInfo> GetNodeTypesByCategory(string category);
    IEnumerable<string> GetCategories();
    NodeTypeInfo? GetNodeTypeInfo(string nodeTypeName);
    
    // Validation
    bool IsRegistered(string nodeTypeName);
    bool IsRegistered<T>() where T : INode;
}

/// <summary>
/// Information about a registered node type
/// </summary>
public class NodeTypeInfo
{
    public string TypeName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public Version Version { get; set; } = new(1, 0, 0);
    public Type NodeType { get; set; } = null!;
    public string[] Tags { get; set; } = [];
    public bool IsExperimental { get; set; }
    
    public override string ToString() => $"{DisplayName} ({Category})";
}