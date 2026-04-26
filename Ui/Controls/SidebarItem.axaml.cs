using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vice.Ui.Controls;

public partial class SidebarItem : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SidebarItem, string>(nameof(Text));

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
                Root.Classes.Set("selected", IsSelected);
            }
        };
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

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