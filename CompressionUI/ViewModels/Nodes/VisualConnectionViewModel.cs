using CompressionUI.Models.Nodes;
using ReactiveUI;
using System;
using Avalonia;

namespace CompressionUI.ViewModels.Nodes;

/// <summary>
/// ViewModel for connections
/// </summary>
public class VisualConnectionViewModel : ReactiveObject
{
    private readonly NodeConnection _connection;

    public VisualConnectionViewModel(NodeConnection connection, VisualPinViewModel output, VisualPinViewModel input)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Input = input ?? throw new ArgumentNullException(nameof(input));
    }

    public NodeConnection Connection => _connection;
    public VisualPinViewModel Output { get; }
    public VisualPinViewModel Input { get; }
}