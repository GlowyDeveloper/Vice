using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;
using System.Linq;
using Vice.Ui.Controls.Effects.Models;
using System;
using Avalonia.Interactivity;

namespace Vice.Ui.Controls.Effects;

public partial class NodeControl : UserControl
{
    private Point _pointerStart;
    private double _startX;
    private double _startY;
    private bool _isDragging;
    private bool _isConnecting;

    public NodeControl()
    {
        InitializeComponent();
        
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        AddHandler(TextBox.TextChangedEvent, OnTextChanged, RoutingStrategies.Bubble);
    }

    public static readonly StyledProperty<NodeEditorModel?> EditorVmProperty =
        AvaloniaProperty.Register<NodeControl, NodeEditorModel?>(nameof(EditorVm));

    public NodeEditorModel? EditorVm
    {
        get => GetValue(EditorVmProperty);
        set => SetValue(EditorVmProperty, value);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            var current = e.GetCurrentPoint(this);
            if (!current.Properties.IsLeftButtonPressed)
                return;

            if (e.Source is Control src && src.DataContext is PortModel port && DataContext is NodeControlModel vm)
            {
                try
                {
                    var ellipse = this.GetVisualDescendants().OfType<Ellipse>().FirstOrDefault(el => el.DataContext is PortModel pm && pm.Id == port.Id);
                    if (ellipse != null)
                    {
                        var center = new Point(ellipse.Bounds.Width / 2.0, ellipse.Bounds.Height / 2.0);
                        var translatedToNode = ellipse.TranslatePoint(center, this);
                        if (translatedToNode != null)
                        {
                            var contentPt = new Point(vm.X + translatedToNode.Value.X, vm.Y + translatedToNode.Value.Y);
                            EditorVm?.BeginPreview(vm.Id, port, contentPt.X, contentPt.Y);
                            _isConnecting = true;
                            e.Pointer.Capture(this);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                const double nodeWidth = 160.0;
                const double portTopOffset = 20.0;
                const double portVerticalSpacing = 18.0;
                const double portEllipseRadius = 5.0;
                const double horizontalMargin = 6.0;

                var startX = vm.X + (port.IsInput ? horizontalMargin + portEllipseRadius : nodeWidth - horizontalMargin - portEllipseRadius);
                var startY = vm.Y + portTopOffset + port.Index * portVerticalSpacing;

                EditorVm?.BeginPreview(vm.Id, port, startX, startY);
                _isConnecting = true;
                e.Pointer.Capture(this);
                return;
            }

            if (DataContext is NodeControlModel nvm)
            {
                _isDragging = true;

                var editor = this.GetVisualAncestors().OfType<NodeEditor>().FirstOrDefault();
                if (editor != null)
                    _pointerStart = editor.ScreenToContent(e.GetPosition(editor));
                else
                    _pointerStart = e.GetPosition(this);

                _startX = nvm.X;
                _startY = nvm.Y;

                e.Pointer.Capture(this);
            }
        }
        catch { }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && DataContext is NodeControlModel vm)
        {
            var editor = this.GetVisualAncestors().OfType<NodeEditor>().FirstOrDefault();
            var pos = editor != null ? editor.ScreenToContent(e.GetPosition(editor)) : e.GetPosition(this);
            var dx = pos.X - _pointerStart.X;
            var dy = pos.Y - _pointerStart.Y;
            var newX = _startX + dx;
            var newY = _startY + dy;

            vm.X = newX;
            vm.Y = newY;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            this.RenderTransform = null;
        }

        if (_isConnecting)
        {
            _isConnecting = false;
            e.Pointer.Capture(null);
        }
    }
    
    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Source is TextBox textBox)
        {
            if (e.Text is null || textBox.Text is null)
            {
                e.Handled = true;
                return;
            }

            foreach (var c in e.Text)
            {
                bool isDigit = c >= '0' && c <= '9';
                bool isDecimalPoint = c == '.' && !textBox.Text.Contains('.');

                if (!isDigit && !isDecimalPoint)
                {
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    private void OnTextChanged(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox)
        {
            var editor = this.GetVisualAncestors().OfType<NodeEditor>().FirstOrDefault();
            editor?.RedrawConnections();
        }
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!(DataContext is NodeControlModel nvm))
                return;

            var vm = EditorVm ?? this.GetVisualAncestors().OfType<NodeEditor>().FirstOrDefault()?.DataContext as NodeEditorModel;
            if (vm == null)
                return;

            var toRemove = vm.Connections?.Where(c => (c?.FromNodeId == nvm.Id) || (c?.ToNodeId == nvm.Id)).ToList() ?? new System.Collections.Generic.List<ConnectionModel>();
            foreach (var c in toRemove)
                vm.Connections?.Remove(c);

            var nodeToRemove = vm.Nodes.FirstOrDefault(n => n.Id == nvm.Id);
            if (nodeToRemove != null)
                vm.Nodes.Remove(nodeToRemove);

            vm.OnEdit();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}
