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
    private ScaleTransform _scaleTransform = new ScaleTransform(1.0, 1.0);
    private TranslateTransform _translateTransform = new TranslateTransform(0.0, 0.0);
    private bool _isPanning;
    private Point _panStartScreen;
    private double _panStartOffsetX;
    private double _panStartOffsetY;

    public NodeEditor()
    {
        InitializeComponent();

        DataContextChanged += NodeEditor_DataContextChanged;
        PointerMoved += NodeEditor_PointerMoved;
        PointerReleased += NodeEditor_PointerReleased;
        PointerWheelChanged += NodeEditor_PointerWheelChanged;
        PointerPressed += NodeEditor_PointerPressed;

        var tg = new TransformGroup();
        tg.Children.Add(_scaleTransform);
        tg.Children.Add(_translateTransform);
        ContentRoot.RenderTransform = tg;

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
        
        UpdateTransforms();

        RedrawConnections();
    }

    private void Nodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (NodeControlModel n in e.NewItems) n.PropertyChanged += Node_PropertyChanged;
        
        Vm?.OnEdit();
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

        if (e.PropertyName == nameof(NodeEditorModel.Zoom) || e.PropertyName == nameof(NodeEditorModel.OffsetX) || e.PropertyName == nameof(NodeEditorModel.OffsetY))
            UpdateTransforms();
    }

    private void Connections_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Vm?.OnEdit();
        RedrawConnections();
    }

    private void NodeEditor_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isPanning && Vm != null)
        {
            var posScreen = e.GetPosition(this);
            var dx = posScreen.X - _panStartScreen.X;
            var dy = posScreen.Y - _panStartScreen.Y;
            Vm.OffsetX = _panStartOffsetX + dx;
            Vm.OffsetY = _panStartOffsetY + dy;
            UpdateTransforms();
            return;
        }

        if (Vm?.IsPreviewing == true)
        {
            var pScreen = e.GetPosition(this);
            var p = ScreenToContent(pScreen);
            Vm.UpdatePreview(p.X, p.Y);
            RedrawConnections();
        }
    }

    private void NodeEditor_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var current = e.GetCurrentPoint(this);
        if (current.Properties.IsMiddleButtonPressed)
        {
            _panStartScreen = e.GetPosition(this);
            _panStartOffsetX = Vm?.OffsetX ?? _translateTransform.X;
            _panStartOffsetY = Vm?.OffsetY ?? _translateTransform.Y;
            try
            {
                e.Pointer.Capture(this);
            }
            catch { }
            _isPanning = true;
            return;
        }
    }

    private void NodeEditor_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (Vm is null) return;

        var posScreen = e.GetPosition(this);
        var before = ScreenToContent(posScreen);

        double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
        var oldZoom = Vm.Zoom;
        var newZoom = Math.Max(0.2, Math.Min(4.0, oldZoom * zoomFactor));

        Vm.Zoom = newZoom;
        Vm.OffsetX = posScreen.X - before.X * newZoom;
        Vm.OffsetY = posScreen.Y - before.Y * newZoom;

        UpdateTransforms();
        RedrawConnections();
    }

    private void NodeEditor_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            return;
        }

        if (Vm?.IsPreviewing == true && Vm.PreviewStartPort != null)
        {
            var point = e.GetPosition(this);
            var contentPoint = ScreenToContent(point);

            PortModel? nearestPort = null;
            double bestDistSq = double.MaxValue;
            const double threshold = 12.0;

            foreach (var node in Vm.Nodes)
            {
                foreach (var p in node.Inputs)
                {
                    var anchor = GetPortAnchor(node.Id, p.Id);
                    if (!anchor.HasValue) continue;
                    var dx = anchor.Value.X - contentPoint.X;
                    var dy = anchor.Value.Y - contentPoint.Y;
                    var distSq = dx * dx + dy * dy;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        nearestPort = p;
                    }
                }

                foreach (var p in node.Outputs)
                {
                    var anchor = GetPortAnchor(node.Id, p.Id);
                    if (!anchor.HasValue) continue;
                    var dx = anchor.Value.X - contentPoint.X;
                    var dy = anchor.Value.Y - contentPoint.Y;
                    var distSq = dx * dx + dy * dy;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        nearestPort = p;
                    }
                }
            }

            if (nearestPort != null && Math.Sqrt(bestDistSq) <= threshold)
            {
                Vm.TryAddConnection(
                    Vm.PreviewStartNodeId!,
                    Vm.PreviewStartPort,
                    nearestPort
                );
            }

            Vm.EndPreview();
            RedrawConnections();
        }
    }

    public void RedrawConnections()
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
        var node = Vm?.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return null;

        var port = node.Inputs.FirstOrDefault(p => p.Id == portId) ?? node.Outputs.FirstOrDefault(p => p.Id == portId);
        if (port is null) return null;

        var nodeControl = this.GetVisualDescendants()
            .OfType<NodeControl>()
            .FirstOrDefault(nc => nc.DataContext is NodeControlModel nvm && nvm.Id == nodeId);

        if (nodeControl != null)
        {
            var ellipse = nodeControl.GetVisualDescendants()
                .OfType<Ellipse>()
                .FirstOrDefault(el => el.DataContext is PortModel pm && pm.Id == portId);

            if (ellipse != null)
            {
                var center = new Point(ellipse.Bounds.Width / 2.0, ellipse.Bounds.Height / 2.0);
                var translatedToNode = ellipse.TranslatePoint(center, nodeControl);
                if (translatedToNode != null)
                {
                    return new Point(node.X + translatedToNode.Value.X, node.Y + translatedToNode.Value.Y);
                }
            }
        }

        const double nodeWidth = 160.0;
        const double horizontalMargin = 6.0;
        const double portEllipseRadius = 5.0;
        const double portTopOffset = 20.0;
        const double portVerticalSpacing = 18.0;

        double x = port.IsInput
            ? node.X + horizontalMargin + portEllipseRadius
            : node.X + nodeWidth - horizontalMargin - portEllipseRadius;

        double y = node.Y + portTopOffset + port.Index * portVerticalSpacing;

        return new Point(x, y);
    }

    private void UpdateTransforms()
    {
        if (Vm is null) return;

        _scaleTransform.ScaleX = Vm.Zoom;
        _scaleTransform.ScaleY = Vm.Zoom;
        _translateTransform.X = Vm.OffsetX;
        _translateTransform.Y = Vm.OffsetY;
    }

    public Point ScreenToContent(Point pScreen)
    {
        var sx = _scaleTransform.ScaleX;
        var sy = _scaleTransform.ScaleY;
        var tx = _translateTransform.X;
        var ty = _translateTransform.Y;

        return new Point((pScreen.X - tx) / sx, (pScreen.Y - ty) / sy);
    }

    public Point ContentToScreen(Point pContent)
    {
        var sx = _scaleTransform.ScaleX;
        var sy = _scaleTransform.ScaleY;
        var tx = _translateTransform.X;
        var ty = _translateTransform.Y;

        return new Point(pContent.X * sx + tx, pContent.Y * sy + ty);
    }
}