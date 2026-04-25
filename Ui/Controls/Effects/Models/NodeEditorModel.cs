using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Linq;

namespace Vice.Ui.Controls.Effects.Models;

public class NodeEditorModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));

    public ObservableCollection<NodeControlModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionModel> Connections { get; } = new();

    private bool _isPreviewing;
    public bool IsPreviewing
    {
        get => _isPreviewing;
        set { if (_isPreviewing != value) { _isPreviewing = value; OnPropertyChanged(); } }
    }
    
    public double PreviewStartX { get; set; }
    public double PreviewStartY { get; set; }
    public double PreviewEndX { get; set; }
    public double PreviewEndY { get; set; }

    public PortModel? PreviewStartPort { get; private set; }
    public string? PreviewStartNodeId { get; private set; }

    public void BeginPreview(string nodeId, PortModel port, double x, double y)
    {
        PreviewStartNodeId = nodeId;
        PreviewStartPort = port;

        PreviewStartX = x;
        PreviewStartY = y;
        PreviewEndX = x;
        PreviewEndY = y;

        IsPreviewing = true;
        OnPropertyChanged(nameof(PreviewStartX));
        OnPropertyChanged(nameof(PreviewStartY));
        OnPropertyChanged(nameof(PreviewEndX));
        OnPropertyChanged(nameof(PreviewEndY));
    }

    public void UpdatePreview(double x, double y)
    {
        PreviewEndX = x;
        PreviewEndY = y;
        OnPropertyChanged(nameof(PreviewEndX));
        OnPropertyChanged(nameof(PreviewEndY));
    }

    public void EndPreview()
    {
        IsPreviewing = false;
        PreviewStartPort = null;
        PreviewStartNodeId = null;
    }

    public void TryAddConnection(string outputNodeId, PortModel output, PortModel input)
    {
        if (output.IsInput || !input.IsInput)
            return;

        if (output.Id == input.Id)
            return;

        if (Connections.Any(c =>
                c.FromNodeId == outputNodeId &&
                c.FromPortId == output.Id &&
                c.ToPortId == input.Id))
            return;

        Connections.Add(new ConnectionModel
        {
            FromNodeId = outputNodeId,
            FromPortId = output.Id,
            ToNodeId = GetNodeId(input)!,
            ToPortId = input.Id
        });
    }

    private string? GetNodeId(PortModel port)
    {
        return Nodes.FirstOrDefault(n =>
            n.Inputs.Any(p => p.Id == port.Id) ||
            n.Outputs.Any(p => p.Id == port.Id)
        )?.Id;
    }

    public NodeEditorModel()
    {
        var n1 = new NodeModel() { Title = "In", X = 50, Y = 50 };
        n1.Outputs.Add(new PortModel() { IsInput = false, Index = 0, Label = "Out" });
        Nodes.Add(new NodeControlModel(n1));
        
        var n2 = new NodeModel() { Title = "Out", X = 200, Y = 50 };
        n2.Inputs.Add(new PortModel() { IsInput = true, Index = 0, Label = "In" });
        Nodes.Add(new NodeControlModel(n2));
        
        TryAddConnection(n1.Id, n1.Outputs[0], n2.Inputs[0]);
    }
}