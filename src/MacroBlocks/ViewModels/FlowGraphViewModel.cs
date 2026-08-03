using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MacroBlocks.Models.Graph;

namespace MacroBlocks.ViewModels;

/// <summary>
/// Presentation logic for the Flow Graph orchestration canvas.
/// </summary>
public sealed class FlowGraphViewModel : ViewModelBase
{
    public const double NodeWidth = 160;
    public const double NodeHeightRun = 72;
    public const double NodeHeightIf = 96;

    private readonly Func<bool> _canEdit;
    private readonly Action<Action> _mutate;
    private readonly Action<string> _setStatus;
    private readonly Action? _onSelectionChanged;
    private FlowGraph _graph = new();
    private FlowGraphNode? _selectedNode;
    private FlowGraphEdge? _selectedEdge;
    private Guid? _wireFromNodeId;
    private FlowGraphPort? _wireFromPort;
    private Point _pendingWireEnd;
    private bool _isWiring;

    public FlowGraphViewModel(
        Func<bool> canEdit,
        Action<Action> mutate,
        Action<string> setStatus,
        Action? onSelectionChanged = null)
    {
        _canEdit = canEdit;
        _mutate = mutate;
        _setStatus = setStatus;
        _onSelectionChanged = onSelectionChanged;

        AddIfNodeCommand = new RelayCommand(
            () => AddIfNode(80, 80),
            () => _canEdit());
        DeleteSelectionCommand = new RelayCommand(
            DeleteSelection,
            () => _canEdit() && (SelectedNode is not null || SelectedEdge is not null));
        CancelWireCommand = new RelayCommand(CancelWire, () => IsWiring);
    }

    public ObservableCollection<FlowGraphNode> Nodes => _graph.Nodes;

    public ObservableCollection<FlowGraphEdge> Edges => _graph.Edges;

    public ObservableCollection<GraphEdgeVisual> EdgeVisuals { get; } = [];

    public FlowGraphNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                if (value is not null)
                {
                    SelectedEdge = null;
                }

                OnPropertyChanged(nameof(HasNodeSelection));
                OnPropertyChanged(nameof(HasIfSelection));
                OnPropertyChanged(nameof(HasRunScriptSelection));
                (DeleteSelectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                _onSelectionChanged?.Invoke();
            }
        }
    }

    public FlowGraphEdge? SelectedEdge
    {
        get => _selectedEdge;
        set
        {
            if (SetProperty(ref _selectedEdge, value))
            {
                if (value is not null)
                {
                    SelectedNode = null;
                }

                OnPropertyChanged(nameof(HasEdgeSelection));
                (DeleteSelectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasNodeSelection => SelectedNode is not null;
    public bool HasIfSelection => SelectedNode?.Kind == FlowGraphNodeKind.If;
    public bool HasRunScriptSelection => SelectedNode?.Kind == FlowGraphNodeKind.RunScript;
    public bool HasEdgeSelection => SelectedEdge is not null;

    public bool IsWiring
    {
        get => _isWiring;
        private set
        {
            if (SetProperty(ref _isWiring, value))
            {
                OnPropertyChanged(nameof(PendingWireVisibility));
                (CancelWireCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public Visibility PendingWireVisibility => IsWiring ? Visibility.Visible : Visibility.Collapsed;

    public Point PendingWireStart { get; private set; }

    public Point PendingWireEnd
    {
        get => _pendingWireEnd;
        set
        {
            _pendingWireEnd = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddIfNodeCommand { get; }
    public ICommand DeleteSelectionCommand { get; }
    public ICommand CancelWireCommand { get; }

    public void Attach(FlowGraph graph)
    {
        _graph.Nodes.CollectionChanged -= OnGraphCollectionChanged;
        _graph.Edges.CollectionChanged -= OnGraphCollectionChanged;
        foreach (var node in _graph.Nodes)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
        }

        _graph = graph;
        SelectedNode = null;
        SelectedEdge = null;
        CancelWire();

        foreach (var node in _graph.Nodes)
        {
            node.PropertyChanged += OnNodePropertyChanged;
        }

        _graph.Nodes.CollectionChanged += OnGraphCollectionChanged;
        _graph.Edges.CollectionChanged += OnGraphCollectionChanged;

        OnPropertyChanged(nameof(Nodes));
        OnPropertyChanged(nameof(Edges));
        RebuildEdgeVisuals();
    }

    public void AddScriptNode(MacroScript script, double x, double y)
    {
        if (!_canEdit())
        {
            return;
        }

        _mutate(() =>
        {
            var node = new FlowGraphNode
            {
                Kind = FlowGraphNodeKind.RunScript,
                ScriptId = script.Id,
                Label = script.Name,
                X = Math.Max(0, x),
                Y = Math.Max(0, y)
            };
            node.PropertyChanged += OnNodePropertyChanged;
            _graph.Nodes.Add(node);
            SelectedNode = node;
            _setStatus($"Added graph node '{script.Name}'");
        });
    }

    public void AddIfNode(double x, double y)
    {
        if (!_canEdit())
        {
            return;
        }

        _mutate(() =>
        {
            var node = new FlowGraphNode
            {
                Kind = FlowGraphNodeKind.If,
                Label = "If",
                X = Math.Max(0, x),
                Y = Math.Max(0, y)
            };
            node.PropertyChanged += OnNodePropertyChanged;
            _graph.Nodes.Add(node);
            SelectedNode = node;
            _setStatus("Added If node");
        });
    }

    public void MoveNode(FlowGraphNode node, double x, double y)
    {
        if (!_canEdit())
        {
            return;
        }

        // Live drag without history spam — caller checkpoints on drag end.
        node.X = Math.Max(0, x);
        node.Y = Math.Max(0, y);
        RebuildEdgeVisuals();
    }

    public void CheckpointMove(Action apply)
    {
        if (!_canEdit())
        {
            return;
        }

        _mutate(apply);
    }

    public void BeginWire(FlowGraphNode from, FlowGraphPort port)
    {
        if (!_canEdit() || !IsValidOutputPort(from, port))
        {
            return;
        }

        _wireFromNodeId = from.Id;
        _wireFromPort = port;
        PendingWireStart = GetPortPoint(from, port);
        PendingWireEnd = PendingWireStart;
        IsWiring = true;
        _setStatus($"Wiring from {from.DisplayTitle} ({port})…");
    }

    public void UpdateWireEnd(Point canvasPoint) => PendingWireEnd = canvasPoint;

    public void CompleteWire(FlowGraphNode to, FlowGraphPort inputPort)
    {
        if (!_canEdit() || _wireFromNodeId is not { } fromId || _wireFromPort is not { } fromPort)
        {
            CancelWire();
            return;
        }

        if (fromId == to.Id)
        {
            CancelWire();
            return;
        }

        if (!TryResolveEdge(fromId, fromPort, to, inputPort, out var edgeFrom, out var edgeTo, out var edgePort))
        {
            _setStatus("Invalid wire connection");
            CancelWire();
            return;
        }

        _mutate(() =>
        {
            // Replace existing edge on the same output port.
            foreach (var existing in _graph.Edges.Where(e => e.FromId == edgeFrom && e.Port == edgePort).ToList())
            {
                _graph.Edges.Remove(existing);
            }

            // Condition into If: only one inbound condition.
            if (edgePort == FlowGraphPort.Condition)
            {
                foreach (var existing in _graph.Edges.Where(e => e.ToId == edgeTo && e.Port == FlowGraphPort.Condition).ToList())
                {
                    _graph.Edges.Remove(existing);
                }
            }

            _graph.Edges.Add(new FlowGraphEdge
            {
                FromId = edgeFrom,
                ToId = edgeTo,
                Port = edgePort
            });
            _setStatus("Connected graph nodes");
        });

        CancelWire();
        RebuildEdgeVisuals();
    }

    public void CancelWire()
    {
        _wireFromNodeId = null;
        _wireFromPort = null;
        IsWiring = false;
    }

    public void SelectNode(FlowGraphNode? node)
    {
        SelectedNode = node;
        if (node is not null)
        {
            SelectedEdge = null;
        }
    }

    public void SelectEdge(FlowGraphEdge? edge)
    {
        SelectedEdge = edge;
        if (edge is not null)
        {
            SelectedNode = null;
        }
    }

    public void AssignSelectedNodeScript(MacroScript? script)
    {
        if (!_canEdit() || SelectedNode is null || SelectedNode.Kind != FlowGraphNodeKind.RunScript)
        {
            return;
        }

        _mutate(() =>
        {
            SelectedNode.ScriptId = script?.Id;
            SelectedNode.Label = script?.Name ?? "(no script)";
            _setStatus(script is null ? "Cleared graph node script" : $"Graph node → '{script.Name}'");
        });
    }

    public void SyncScriptLabels(IEnumerable<MacroScript> library)
    {
        var map = library.ToDictionary(s => s.Id, s => s.Name);
        foreach (var node in _graph.Nodes.Where(n => n.Kind == FlowGraphNodeKind.RunScript))
        {
            if (node.ScriptId is { } id && map.TryGetValue(id, out var name))
            {
                node.Label = name;
            }
            else if (node.ScriptId is not null)
            {
                node.Label = "(missing script)";
            }
        }
    }

    private void DeleteSelection()
    {
        if (!_canEdit())
        {
            return;
        }

        if (SelectedNode is { } node)
        {
            _mutate(() =>
            {
                node.PropertyChanged -= OnNodePropertyChanged;
                foreach (var edge in _graph.Edges.Where(e => e.FromId == node.Id || e.ToId == node.Id).ToList())
                {
                    _graph.Edges.Remove(edge);
                }

                _graph.Nodes.Remove(node);
                SelectedNode = null;
                _setStatus("Removed graph node");
            });
            RebuildEdgeVisuals();
            return;
        }

        if (SelectedEdge is { } edge)
        {
            _mutate(() =>
            {
                _graph.Edges.Remove(edge);
                SelectedEdge = null;
                _setStatus("Removed graph wire");
            });
            RebuildEdgeVisuals();
        }
    }

    private static bool IsValidOutputPort(FlowGraphNode from, FlowGraphPort port)
        => from.Kind switch
        {
            FlowGraphNodeKind.RunScript => port is FlowGraphPort.Next or FlowGraphPort.Condition,
            FlowGraphNodeKind.If => port is FlowGraphPort.Then or FlowGraphPort.Else,
            _ => false
        };

    private static bool TryResolveEdge(
        Guid fromId,
        FlowGraphPort fromPort,
        FlowGraphNode to,
        FlowGraphPort clickedPort,
        out Guid edgeFrom,
        out Guid edgeTo,
        out FlowGraphPort edgePort)
    {
        edgeFrom = fromId;
        edgeTo = to.Id;
        edgePort = fromPort;

        // Dragging Next/Then/Else onto a node body or Next input → sequence into that node.
        if (fromPort is FlowGraphPort.Next or FlowGraphPort.Then or FlowGraphPort.Else)
        {
            if (clickedPort is FlowGraphPort.Next or FlowGraphPort.Then or FlowGraphPort.Else
                || clickedPort == FlowGraphPort.Condition && to.Kind != FlowGraphNodeKind.If)
            {
                // Sequence edges store Port as the output port on the source.
                edgePort = fromPort;
                return true;
            }

            // Explicit: Next onto Condition port of If is invalid for sequence; use Condition output.
            if (clickedPort == FlowGraphPort.Condition && to.Kind == FlowGraphNodeKind.If)
            {
                // Allow completing a Condition wire only when fromPort is Condition.
                return false;
            }

            edgePort = fromPort;
            return true;
        }

        // Condition output must land on an If node's condition input.
        if (fromPort == FlowGraphPort.Condition)
        {
            if (to.Kind != FlowGraphNodeKind.If)
            {
                return false;
            }

            edgePort = FlowGraphPort.Condition;
            return true;
        }

        return false;
    }

    private void OnGraphCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (FlowGraphNode node in e.NewItems.OfType<FlowGraphNode>())
            {
                node.PropertyChanged -= OnNodePropertyChanged;
                node.PropertyChanged += OnNodePropertyChanged;
            }
        }

        RebuildEdgeVisuals();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FlowGraphNode.X) or nameof(FlowGraphNode.Y) or nameof(FlowGraphNode.Label))
        {
            RebuildEdgeVisuals();
        }
    }

    public void RebuildEdgeVisuals()
    {
        EdgeVisuals.Clear();
        var nodes = _graph.Nodes.ToDictionary(n => n.Id);
        foreach (var edge in _graph.Edges)
        {
            if (!nodes.TryGetValue(edge.FromId, out var from) || !nodes.TryGetValue(edge.ToId, out var to))
            {
                continue;
            }

            var start = GetPortPoint(from, edge.Port == FlowGraphPort.Condition ? FlowGraphPort.Condition : edge.Port);
            // Condition edges leave the Boolean out port on the RunScript and enter If's condition.
            if (edge.Port == FlowGraphPort.Condition)
            {
                start = GetPortPoint(from, FlowGraphPort.Condition);
            }

            var endPort = edge.Port switch
            {
                FlowGraphPort.Condition => FlowGraphPort.Condition,
                _ => FlowGraphPort.Next // enter left/center of target
            };
            var end = GetPortPoint(to, endPort == FlowGraphPort.Condition ? FlowGraphPort.Condition : FlowGraphPort.Next, isInput: true);

            EdgeVisuals.Add(new GraphEdgeVisual(edge, start, end));
        }

        OnPropertyChanged(nameof(EdgeVisuals));
    }

    public static Point GetPortPoint(FlowGraphNode node, FlowGraphPort port, bool isInput = false)
    {
        var h = node.Kind == FlowGraphNodeKind.If ? NodeHeightIf : NodeHeightRun;
        var w = NodeWidth;
        return (port, isInput, node.Kind) switch
        {
            (FlowGraphPort.Next, false, _) => new Point(node.X + w, node.Y + h / 2),
            (FlowGraphPort.Condition, false, FlowGraphNodeKind.RunScript) => new Point(node.X + w, node.Y + h * 0.75),
            (FlowGraphPort.Then, false, _) => new Point(node.X + w, node.Y + h * 0.35),
            (FlowGraphPort.Else, false, _) => new Point(node.X + w, node.Y + h * 0.7),
            (FlowGraphPort.Condition, true, _) => new Point(node.X, node.Y + h * 0.2),
            (_, true, _) => new Point(node.X, node.Y + h / 2),
            _ => new Point(node.X + w / 2, node.Y + h / 2)
        };
    }
}

/// <summary>
/// Line geometry for a graph edge on the canvas.
/// </summary>
public sealed class GraphEdgeVisual : ViewModelBase
{
    public GraphEdgeVisual(FlowGraphEdge edge, Point start, Point end)
    {
        Edge = edge;
        Start = start;
        End = end;
    }

    public FlowGraphEdge Edge { get; }
    public Point Start { get; }
    public Point End { get; }
    public double X1 => Start.X;
    public double Y1 => Start.Y;
    public double X2 => End.X;
    public double Y2 => End.Y;
}
