using System;
using System.ComponentModel;

namespace CompressionUI.Models.Nodes;

public enum PropertyType
{
    String,
    Integer,
    Float,
    Boolean,
    FilePath,
    DirectoryPath,
    Enum,
    Range
}

/// <summary>
/// Represents a configurable property of a node
/// </summary>
public class NodeProperty : INotifyPropertyChanged
{
    private object? _value;
    
    public string Id { get; }
    public string Name { get; set; }
    public string Description { get; set; }
    public PropertyType Type { get; }
    public object? DefaultValue { get; }
    public bool IsRequired { get; set; }
    public bool IsReadOnly { get; set; }

    // Constraints
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public string[]? EnumValues { get; set; }
    public string? FileFilter { get; set; } // For file dialogs

    public object? Value
    {
        get => _value ?? DefaultValue;
        set
        {
            if (IsReadOnly) return;
            
            var newValue = ValidateAndConvertValue(value);
            if (!Equals(_value, newValue))
            {
                _value = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public NodeProperty(string id, string name, PropertyType type, object? defaultValue = null)
    {
        Id = id;
        Name = name;
        Type = type;
        DefaultValue = defaultValue;
        Description = "";
        _value = defaultValue;
    }

    private object? ValidateAndConvertValue(object? value)
    {
        if (value == null && !IsRequired)
            return null;

        try
        {
            return Type switch
            {
                PropertyType.String => value?.ToString() ?? "",
                PropertyType.Integer => Convert.ToInt32(value),
                PropertyType.Float => Convert.ToSingle(value),
                PropertyType.Boolean => Convert.ToBoolean(value),
                PropertyType.FilePath => value?.ToString() ?? "",
                PropertyType.DirectoryPath => value?.ToString() ?? "",
                PropertyType.Enum => ValidateEnumValue(value?.ToString()),
                PropertyType.Range => ValidateRangeValue(value),
                _ => value
            };
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid value for property '{Name}': {ex.Message}", ex);
        }
    }

    private string? ValidateEnumValue(string? value)
    {
        if (EnumValues == null || value == null) return value;
        
        if (Array.IndexOf(EnumValues, value) == -1)
        {
            throw new ArgumentException($"Value '{value}' is not valid. Valid values: {string.Join(", ", EnumValues)}");
        }
        
        return value;
    }

    private object? ValidateRangeValue(object? value)
    {
        if (value == null) return null;
        
        var numericValue = Convert.ToSingle(value);
        
        if (MinValue != null && numericValue < Convert.ToSingle(MinValue))
            return MinValue;
            
        if (MaxValue != null && numericValue > Convert.ToSingle(MaxValue))
            return MaxValue;
            
        return numericValue;
    }

    public T? GetValue<T>()
    {
        if (Value is T typedValue)
            return typedValue;
            
        if (Value != null && typeof(T).IsAssignableFrom(Value.GetType()))
            return (T)Value;
            
        return default(T);
    }

    public override string ToString() => $"{Name}: {Value}";
}