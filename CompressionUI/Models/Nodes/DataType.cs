using System;
using System.Collections.Generic;

namespace CompressionUI.Models.Nodes;

/// <summary>
/// Represents the type of data that can flow between nodes
/// </summary>
public class DataType
{
    public string Name { get; }
    public Type ClrType { get; }
    public string Description { get; }
    public bool IsGeneric { get; }

    public DataType(string name, Type clrType, string description = "", bool isGeneric = false)
    {
        Name = name;
        ClrType = clrType;
        Description = description;
        IsGeneric = isGeneric;
    }

    public bool IsCompatibleWith(DataType other)
    {
        if (this == other) return true;
        if (Name == other.Name) return true;
        
        // Check for inheritance/interface compatibility
        return ClrType.IsAssignableFrom(other.ClrType) || 
               other.ClrType.IsAssignableFrom(ClrType);
    }

    public override string ToString() => Name;
    public override bool Equals(object? obj) => obj is DataType dt && dt.Name == Name;
    public override int GetHashCode() => Name.GetHashCode();
}

/// <summary>
/// Standard data types used in the node system
/// </summary>
public static class DataTypes
{
    public static readonly DataType String = new("String", typeof(string), "Text data");
    public static readonly DataType Integer = new("Integer", typeof(int), "Whole numbers");
    public static readonly DataType Float = new("Float", typeof(float), "Decimal numbers");
    public static readonly DataType Boolean = new("Boolean", typeof(bool), "True/False values");
    public static readonly DataType FilePath = new("FilePath", typeof(string), "File system paths");
    
    // ML-specific types
    public static readonly DataType Tensor = new("Tensor", typeof(object), "Multi-dimensional arrays", true);
    public static readonly DataType Model = new("Model", typeof(object), "Machine learning models", true);
    public static readonly DataType Dataset = new("Dataset", typeof(object), "Training datasets", true);
    
    // Generic data type for any object
    public static readonly DataType Any = new("Any", typeof(object), "Any data type", true);

    public static readonly DataType[] All = {
        String, Integer, Float, Boolean, FilePath,
        Tensor, Model, Dataset, Any
    };

    public static DataType? FromString(string name) =>
        Array.Find(All, dt => dt.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}