using CompressionUI.Models.Nodes;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;

namespace CompressionUI.ViewModels.Nodes;

/// <summary>
/// ViewModel wrapper for INode to work with NodifyAvalonia
/// </summary>
public class VisualNodeViewModel : ReactiveObject
{
    private readonly INode _node;
    private Point _location;
    private bool _isSelected;
    private Size _size;

    public VisualNodeViewModel(INode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _location = new Point(node.X, node.Y);
        _size = new Size(150, 100); // Default size

        // Initialize pin collections
        Input = new ObservableCollection<VisualPinViewModel>();
        Output = new ObservableCollection<VisualPinViewModel>();

        // Create visual representations of pins
        foreach (var inputPin in node.InputPins)
        {
            Input.Add(new VisualPinViewModel(inputPin));
        }

        foreach (var outputPin in node.OutputPins)
        {
            Output.Add(new VisualPinViewModel(outputPin));
        }

        // React to location changes
        this.WhenAnyValue(x => x.Location)
            .Subscribe(location => 
            {
                _node.X = location.X;
                _node.Y = location.Y;
            });

        // React to node state changes
        _node.StateChanged += OnNodeStateChanged;
    }

    public INode Node => _node;
    public string Title => _node.Name;
    public string Description => _node.Description;

    // Properties for NodifyAvalonia
    public Point Location
    {
        get => _location;
        set => this.RaiseAndSetIfChanged(ref _location, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public Size Size
    {
        get => _size;
        set => this.RaiseAndSetIfChanged(ref _size, value);
    }

    public ObservableCollection<VisualPinViewModel> Input { get; }
    public ObservableCollection<VisualPinViewModel> Output { get; }

    public string StatusText => _node.State switch
    {
        NodeExecutionState.Idle => "Ready",
        NodeExecutionState.Executing => "Running...",
        NodeExecutionState.Completed => $"Completed",
        NodeExecutionState.Error => $"Error",
        _ => "Unknown"
    };

    private void OnNodeStateChanged(object? sender, EventArgs e)
    {
        this.RaisePropertyChanged(nameof(StatusText));
    }

    public void Dispose()
    {
        _node.StateChanged -= OnNodeStateChanged;
        _node.Dispose();
    }
}