using System.Collections.Generic;
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
        AvaloniaProperty.Register<KeybindListener, string>(nameof(Text), "Click to bind");

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
            Text = KeyGesture != null ? FormatKeyGesture(KeyGesture) : "Click to bind";
            return;
        }
        
        if (e.Key == Key.Back)
        {
            KeyGesture = null;
            _isListening = false;
            Text = "Click to bind";
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
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LWin || key == Key.RWin;
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
            Text = gesture != null ? FormatKeyGesture(gesture) : "Click to bind";
        }
    }

    private string FormatKeyGesture(KeyGesture gesture)
    {
        var parts = new List<string>();

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Win");

        parts.Add(KeyToString(gesture.Key));

        return string.Join(" + ", parts);
    }

    private string KeyToString(Key key)
    {
        return key switch
        {
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.Oem1 => ";",
            Key.Oem2 => "/",
            Key.Oem3 => "`",
            Key.Oem4 => "[",
            Key.Oem5 => "\\",
            Key.Oem6 => "]",
            Key.Oem7 => "'",
            Key.Oem8 => "`",

            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",

            Key.NumPad0 => "NumPad0",
            Key.NumPad1 => "NumPad1",
            Key.NumPad2 => "NumPad2",
            Key.NumPad3 => "NumPad3",
            Key.NumPad4 => "NumPad4",
            Key.NumPad5 => "NumPad5",
            Key.NumPad6 => "NumPad6",
            Key.NumPad7 => "NumPad7",
            Key.NumPad8 => "NumPad8",
            Key.NumPad9 => "NumPad9",

            Key.Return => "Enter",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Delete => "Del",

            _ => key.ToString()
        };
    }
}