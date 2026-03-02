using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Vice.Ui.Controls;

public partial class KeybindListener : Border
{
    private bool _isListening;

    public static readonly StyledProperty<KeyGesture?> KeyGestureProperty =
        AvaloniaProperty.Register<KeybindListener, KeyGesture?>(nameof(KeyGesture));
    
    public static readonly StyledProperty<string> TextProperty = 
        AvaloniaProperty.Register<KeybindListener, string>(nameof(Text));

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public KeyGesture? KeyGesture
    {
        get => GetValue(KeyGestureProperty);
        set => SetValue(KeyGestureProperty, value);
    }
    
    public KeybindListener()
    {
        InitializeComponent();

        PointerPressed += (_, _) =>
        {
            _isListening = true;
            Text = "Press key combo...";
            Focus();
        };

        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isListening)
            return;

        if (e.Key == Key.Escape)
        {
            _isListening = false;
            return;
        }
        
        if (e.Key == Key.Back)
        {
            KeyGesture = null;
            _isListening = false;
            return;
        }

        if (IsModifierKey(e.Key))
            return;

        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        KeyGesture = gesture;
        
        _isListening = false;
        e.Handled = true;
    }

    private bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftShift || key == Key.RightShift ||
               key == Key.LeftAlt || key == Key.RightAlt;
    }
    
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        _isListening = false;
        base.OnLostFocus(e);
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == KeyGestureProperty)
        {
            var gesture = change.GetNewValue<KeyGesture?>();
            Text = gesture?.ToString() ?? "Click to bind";
        }
    }
}