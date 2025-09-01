using CompressionUI.Models.Nodes;
using ReactiveUI;
using System;
using System.Drawing;
using Avalonia;
using Point = Avalonia.Point;

namespace CompressionUI.ViewModels.Nodes;

/// <summary>
/// ViewModel for node pins
/// </summary>
public class VisualPinViewModel : ReactiveObject
{
    private readonly NodePin _pin;
    private Point _anchor;

    public VisualPinViewModel(NodePin pin)
    {
        _pin = pin ?? throw new ArgumentNullException(nameof(pin));
        _pin.ValueChanged += OnValueChanged;
    }

    public NodePin Pin => _pin;
    public string Title => _pin.Name;
    public bool IsConnected => _pin.IsConnected;
    public object? Value => _pin.Value;

    public Point Anchor
    {
        get => _anchor;
        set => this.RaiseAndSetIfChanged(ref _anchor, value);
    }

    private void OnValueChanged(object? sender, EventArgs e)
    {
        this.RaisePropertyChanged(nameof(IsConnected));
        this.RaisePropertyChanged(nameof(Value));
    }

    public void Dispose()
    {
        _pin.ValueChanged -= OnValueChanged;
    }
}