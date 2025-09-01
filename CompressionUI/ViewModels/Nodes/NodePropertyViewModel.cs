using System.Reactive.Linq;
using CompressionUI.Models.Nodes;
using ReactiveUI;

namespace CompressionUI.ViewModels.Nodes;

/// <summary>
/// ViewModel for node properties in the property panel
/// </summary>
public class NodePropertyViewModel : ReactiveObject
{
    private readonly NodeProperty _property;
    private object? _value;

    public NodePropertyViewModel(NodeProperty property)
    {
        _property = property ?? throw new ArgumentNullException(nameof(property));
        _value = property.Value;

        // React to value changes and update the underlying property
        this.WhenAnyValue(x => x.Value)
            .Skip(1) // Skip initial value
            .Subscribe(value => _property.Value = value);

        // React to property changes from the model
        _property.PropertyChanged += OnPropertyChanged;
    }

    public NodeProperty Property => _property;

    public string Id => _property.Id;
    public string Name => _property.Name;
    public string DisplayName => _property.Name; // TODO: add DisplayName to NodeProperty
    public string Description => _property.Description;
    public PropertyType PropertyType => _property.Type;
    public bool IsReadOnly => _property.IsReadOnly;

    public object? Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public string? StringValue
    {
        get => Value?.ToString();
        set => Value = value;
    }

    public int IntValue
    {
        get => Value is int i ? i : 0;
        set => Value = value;
    }

    public float FloatValue
    {
        get => Value is float f ? f : 0f;
        set => Value = value;
    }

    public bool BoolValue
    {
        get => Value is bool b && b;
        set => Value = value;
    }

    public string[] EnumValues => _property.EnumValues ?? Array.Empty<string>();
    public string? FileFilter => _property.FileFilter;

    public bool IsStringType => PropertyType == PropertyType.String;
    public bool IsIntegerType => PropertyType == PropertyType.Integer;
    public bool IsFloatType => PropertyType == PropertyType.Float;
    public bool IsBooleanType => PropertyType == PropertyType.Boolean;
    public bool IsEnumType => PropertyType == PropertyType.Enum;
    public bool IsFilePathType => PropertyType == PropertyType.FilePath;
    public bool IsFolderPathType => PropertyType == PropertyType.DirectoryPath;

    private void OnPropertyChanged(object? sender, EventArgs e)
    {
        Value = _property.Value;
    }

    public void Dispose()
    {
        _property.PropertyChanged -= OnPropertyChanged;
    }
}