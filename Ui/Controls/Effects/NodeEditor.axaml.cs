using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vice.Ui.Controls.Effects.Models;

namespace Vice.Ui.Controls.Effects;

public partial class NodeEditor : UserControl
{
    private NodeEditorModel? Vm => DataContext as NodeEditorModel;
    private bool _needsInitialDraw = true;

    public NodeEditor()
    {
        InitializeComponent();

        DataContextChanged += NodeEditor_DataContextChanged;
        PointerMoved += NodeEditor_PointerMoved;
        PointerReleased += NodeEditor_PointerReleased;

        LayoutUpdated += (_, __) =>
        {
            if (_needsInitialDraw)
            {
                _needsInitialDraw = false;
                RedrawConnections();
            }
        };
    }

    private void NodeEditor_DataContextChanged(object? sender, EventArgs e)
    {
        if (Vm is null) return;
        
        Vm.Connections.CollectionChanged += Connections_CollectionChanged;
        Vm.Nodes.CollectionChanged += Nodes_CollectionChanged;
        Vm.PropertyChanged += Vm_PropertyChanged;
        
        foreach (var n in Vm.Nodes)
            n.PropertyChanged += Node_PropertyChanged;
        
        RedrawConnections();
    }

    private void Nodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (NodeControlModel n in e.NewItems) n.PropertyChanged += Node_PropertyChanged;
        
        RedrawConnections();
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is NodeControlModel nvm && (e.PropertyName == nameof(NodeControlModel.X) || e.PropertyName == nameof(NodeControlModel.Y)))
        {
            RedrawConnections();
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeEditorModel.IsPreviewing) || e.PropertyName == nameof(NodeEditorModel.PreviewEndX) || e.PropertyName == nameof(NodeEditorModel.PreviewEndY))
            RedrawConnections();
    }

    private void Connections_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RedrawConnections();
    }

    private void NodeEditor_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (Vm?.IsPreviewing == true)
        {
            var p = e.GetPosition(this);
            Vm.UpdatePreview(p.X, p.Y);
            RedrawConnections();
        }
    }

    private void NodeEditor_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Vm?.IsPreviewing == true && Vm.PreviewStartPort != null)
        {
            var point = e.GetPosition(this);

            var hit = this.GetVisualsAt(point)
                .OfType<Control>()
                .FirstOrDefault(c => c.DataContext is PortModel);

            var targetPort = hit?.DataContext as PortModel;

            if (targetPort != null)
            {
                Vm.TryAddConnection(
                    Vm.PreviewStartNodeId!,
                    Vm.PreviewStartPort,
                    targetPort
                );
            }

            Vm.EndPreview();
            RedrawConnections();
        }
    }

    private void RedrawConnections()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConnectionsLayer.Children.Clear();
            if (Vm is null) return;

            foreach (var c in Vm.Connections)
            {
                var a = GetPortAnchor(c.FromNodeId, c.FromPortId);
                var b = GetPortAnchor(c.ToNodeId, c.ToPortId);

                if (a.HasValue && b.HasValue)
                    ConnectionsLayer.Children.Add(
                        CreateBezier(a.Value, b.Value, Brushes.LightBlue, 3.0)
                    );
            }

            if (Vm.IsPreviewing)
            {
                var start = new Point(Vm.PreviewStartX, Vm.PreviewStartY);
                var end = new Point(Vm.PreviewEndX, Vm.PreviewEndY);

                ConnectionsLayer.Children.Add(
                    CreateBezier(start, end, Brushes.LightGray, 2.0)
                );
            }
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    private Control CreateBezier(Point a, Point b, IBrush stroke, double thickness)
    {
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            var cp1 = new Point(a.X + (b.X - a.X) * 0.5, a.Y);
            var cp2 = new Point(a.X + (b.X - a.X) * 0.5, b.Y);
            
            ctx.BeginFigure(a, false);
            ctx.CubicBezierTo(cp1, cp2, b);
        }

        var path = new Path
        {
            Stroke = stroke,
            StrokeThickness = thickness,
            Data = geom
        };

        return path;
    }
    
    private Point? GetPortAnchor(string nodeId, string portId)
    {
        var nodeControl = this.GetVisualDescendants()
            .OfType<NodeControl>()
            .FirstOrDefault(nc => nc.DataContext is NodeControlModel nvm && nvm.Id == nodeId);

        if (nodeControl == null)
            return null;

        var ellipse = nodeControl.GetVisualDescendants()
            .OfType<Ellipse>()
            .FirstOrDefault(el => el.DataContext is PortModel pm && pm.Id == portId);

        if (ellipse == null)
            return null;

        var center = new Point(
            ellipse.Bounds.Width / 2.0,
            ellipse.Bounds.Height / 2.0
        );

        var translated = ellipse.TranslatePoint(center, this);

        if (translated != null)
            return translated.Value;

        var node = Vm?.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return null;

        var port = node.Inputs.FirstOrDefault(p => p.Id == portId) ?? node.Outputs.FirstOrDefault(p => p.Id == portId);
        if (port is null) return null;
        
        double x = port.IsInput
            ? node.X + 6 + 5
            : node.X + 160 - 6 - 5;

        double y = node.Y + 20 + port.Index * 18;

        return new Point(x, y);
    }
}