using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Vice.Ui.Controls;

public partial class SidebarItem : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SidebarItem, string>(nameof(Text));

    //public static readonly StyledProperty<Control?> IconProperty =
    //    AvaloniaProperty.Register<SidebarItem, Control?>(nameof(Icon));
    
    public static readonly StyledProperty<Geometry> IconProperty =
        AvaloniaProperty.Register<SidebarItem, Geometry>(nameof(Icon));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SidebarItem, bool>(nameof(IsSelected));

    public SidebarItem()
    {
        InitializeComponent();
        
        this.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsSelectedProperty)
            {
                Root.Background = IsSelected
                    ? new SolidColorBrush(Color.Parse("#2A2A2A"))
                    : new SolidColorBrush(Colors.Transparent);
            }
        };

        Root.PointerMoved += (_, _) =>
        {
            Root.Background = new SolidColorBrush(Color.Parse("#2A2A2A"));
        };

        Root.PointerExited += (_, _) =>
        {
            if (GetValue(IsSelectedProperty))
            {
                Root.Background = new SolidColorBrush(Color.Parse("#2A2A2A"));
            }
            else
            {
                Root.Background = new SolidColorBrush(Colors.Transparent);
            }
        };
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    //public Control? Icon
    public Geometry Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }
}