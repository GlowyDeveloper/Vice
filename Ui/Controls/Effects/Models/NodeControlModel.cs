using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace Vice.Ui.Controls.Effects.Models;

public class NodeControlModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));

    private readonly NodeModel _model;
    public NodeControlModel(NodeModel model)
    {
        _model = model;
    }

    public string Id => _model.Id;
    public string Title { get => _model.Title; set { if (_model.Title != value) { _model.Title = value; OnPropertyChanged(); } } }
    public double X { get => _model.X; set { if (_model.X != value) { _model.X = value; OnPropertyChanged(); } } }
    public double Y { get => _model.Y; set { if (_model.Y != value) { _model.Y = value; OnPropertyChanged(); } } }

    public ObservableCollection<PortModel> Inputs => _model.Inputs;
    public ObservableCollection<PortModel> Outputs => _model.Outputs;
    public ObservableCollection<OptionModel> Options => _model.Options;
}
