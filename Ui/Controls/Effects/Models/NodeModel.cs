using System;
using System.Collections.ObjectModel;

namespace Vice.Ui.Controls.Effects.Models;

public class NodeModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "Node";
    public double X { get; set; }
    public double Y { get; set; }
    public ObservableCollection<PortModel> Inputs { get; } = new();
    public ObservableCollection<PortModel> Outputs { get; } = new();
    public ObservableCollection<OptionModel> Options { get; } = new();
}
