using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;
using System.Linq;
using Vice.Ui.Controls.Effects.Models;
using System;

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
        if (e.Source is Control src && src.DataContext is PortModel port && DataContext is NodeControlModel vm)
        {
            try
            {
                var ellipse = this.GetVisualDescendants().OfType<Ellipse>().FirstOrDefault(el => el.DataContext is PortModel pm && pm.Id == port.Id);
                var editor = this.GetVisualAncestors().OfType<NodeEditor>().FirstOrDefault();
                
                if (ellipse != null && editor != null)
                {
                    var center = new Point(ellipse.Bounds.Width / 2.0, ellipse.Bounds.Height / 2.0);
                    var translated = ellipse.TranslatePoint(center, editor);
                    
                    if (translated != null)
                    {
                        EditorVm?.BeginPreview(vm.Id, port, translated.Value.X, translated.Value.Y);
                        
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
                _pointerStart = e.GetPosition(editor);
            else
                _pointerStart = e.GetPosition(this);
            
            _startX = nvm.X;
            _startY = nvm.Y;
            
            e.Pointer.Capture(this);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && DataContext is NodeControlModel vm)
        {
            var editor = this.GetVisualAncestors().OfType<NodeEditor>().FirstOrDefault();
            var pos = editor != null ? e.GetPosition(editor) : e.GetPosition(this);
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
}
