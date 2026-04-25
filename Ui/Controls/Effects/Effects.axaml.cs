using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Vice.Ui.Controls.Effects.Models;
using Vice.Ui.Utils;

namespace Vice.Ui.Controls.Effects;

public partial class Effects : UserControl, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    
    public NodeEditorModel EditorVm { get; set; } = new();

    public Effects()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void ConvertJson(EffectsClass EffectsPath)
    {
        EditorVm.Nodes.Clear();
        EditorVm.Connections.Clear();
        
        foreach (var node in EffectsPath.nodes)
        {
            if (Enum.TryParse<NodeType>(node.type_of, true, out var type))
            {
                var model = new NodeModel() { Title = node.type_of, X = node.x, Y = node.y, Id = node.id };
                model.Inputs.Add(new PortModel() { IsInput = true, Index = 0, Label = "In", Id = node.inputs.Count > 0 ? node.inputs[0] : Guid.NewGuid().ToString() });
                model.Outputs.Add(new PortModel() { IsInput = false, Index = 0, Label = "Out", Id = node.outputs.Count > 0 ? node.outputs[0] : Guid.NewGuid().ToString() });
                
                switch (type)
                {
                    case NodeType.In:
                        model.Inputs.Clear();
                        break;
                    case NodeType.Out:
                        model.Outputs.Clear();
                        break;
                    case NodeType.Split:
                        model.Outputs.Clear();
                
                        model.Outputs.Add(new PortModel() { IsInput = false, Index = 0, Label = "L", Id = node.outputs.Count > 0 ? node.outputs[0] : Guid.NewGuid().ToString() });
                        model.Outputs.Add(new PortModel() { IsInput = false, Index = 1, Label = "R", Id = node.outputs.Count > 1 ? node.outputs[1] : Guid.NewGuid().ToString() });
                        break;
                    case NodeType.Merge:
                        model.Inputs.Clear();
                
                        model.Inputs.Add(new PortModel() { IsInput = true, Index = 0, Label = "L", Id = node.inputs.Count > 0 ? node.inputs[0] : Guid.NewGuid().ToString() });
                        model.Inputs.Add(new PortModel() { IsInput = true, Index = 1, Label = "R", Id = node.inputs.Count > 1 ? node.inputs[1] : Guid.NewGuid().ToString() });
                        break;
                    case NodeType.Compression:
                        model.Options.Add(new OptionModel() { Title = "Amount (%)", CurrentInput = node.options.Count > 0 ? node.options[0] : string.Empty });
                        break;
                    case NodeType.Delay:
                        model.Options.Add(new OptionModel() { Title = "Time (ms)", CurrentInput = node.options.Count > 0 ? node.options[0] : string.Empty });
                        break;
                    case NodeType.Distortion:
                        model.Options.Add(new OptionModel() { Title = "Intensity (%)", CurrentInput = node.options.Count > 0 ? node.options[0] : string.Empty });
                        break;
                    case NodeType.Gain:
                        model.Options.Add(new OptionModel() { Title = "Gain (x)", CurrentInput = node.options.Count > 0 ? node.options[0] : string.Empty });
                        break;
                    case NodeType.Gating:
                        model.Options.Add(new OptionModel() { Title = "Threshold (%)", CurrentInput = node.options.Count > 0 ? node.options[0] : string.Empty });
                        break;
                    case NodeType.Reverb:
                        model.Options.Add(new OptionModel() { Title = "Intensity (%)", CurrentInput = node.options.Count > 0 ? node.options[0] : string.Empty });
                        break;
                }
        
                EditorVm.Nodes.Add(new NodeControlModel(model));
            }
        }

        foreach (var connection in EffectsPath.connections)
        {
            var fromNode = EditorVm.Nodes.FirstOrDefault(n => n.Id == connection.from_node_id);
            var toNode = EditorVm.Nodes.FirstOrDefault(n => n.Id == connection.to_node_id);

            if (fromNode == null || toNode == null)
                continue;

            var outputPort = fromNode.Outputs.FirstOrDefault(p => p.Id == connection.from_port_id);
            var inputPort = toNode.Inputs.FirstOrDefault(p => p.Id == connection.to_port_id);

            if (outputPort == null || inputPort == null)
                continue;

            EditorVm.Connections.Add(new ConnectionModel
            {
                FromNodeId = fromNode.Id,
                FromPortId = outputPort.Id,
                ToNodeId = toNode.Id,
                ToPortId = inputPort.Id
            });
        }
    }

    public EffectsClass GetCurrentJson()
    {
        var nodes = new List<NodeClass>();
        foreach (var node in EditorVm.Nodes)
        {
            var inputs = new List<String>();
            foreach (var port in node.Inputs)
            {
                inputs.Add(port.Id);
            }
            
            var outputs = new List<String>();
            foreach (var port in node.Outputs)
            {
                outputs.Add(port.Id);
            }
            
            var options = new List<String>();
            foreach (var port in node.Options)
            {
                options.Add(port.CurrentInput);
            }
            
            var nodeClass = new NodeClass() { id = node.Id, x = (int)node.X, y = (int)node.Y, type_of = node.Title.ToString(), inputs = inputs, outputs = outputs, options = options };
            
            nodes.Add(nodeClass);
        }
        
        var connections = new List<ConnectionClass>();
        foreach (var connection in EditorVm.Connections)
        {
            connections.Add(new ConnectionClass() { from_node_id = connection.FromNodeId, to_node_id = connection.ToNodeId, from_port_id = connection.FromPortId, to_port_id = connection.ToPortId });
        }
        
        return new EffectsClass() { nodes = nodes, connections = connections };
    }

    public void Reset()
    {
        EditorVm = new NodeEditorModel();
        OnPropertyChanged(nameof(EditorVm));
    }

    private void AddNodeButton(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            if (Enum.TryParse<NodeType>(button.Content!.ToString(), true, out var type))
            {
                AddNode(type);
            }
        }
    }

    private void AddNode(NodeType nodeType)
    {
        var node = new NodeModel() { Title = nodeType.ToString(), X = 50, Y = 50 };
        node.Inputs.Add(new PortModel() { IsInput = true, Index = 0, Label = "In" });
        node.Outputs.Add(new PortModel() { IsInput = false, Index = 0, Label = "Out" });
        
        switch (nodeType)
        {
            case NodeType.In:
                node.Inputs.Clear();
                break;
            case NodeType.Out:
                node.Outputs.Clear();
                break;
            case NodeType.Split:
                node.Outputs.Clear();
                
                node.Outputs.Add(new PortModel() { IsInput = false, Index = 0, Label = "L" });
                node.Outputs.Add(new PortModel() { IsInput = false, Index = 1, Label = "R" });
                break;
            case NodeType.Merge:
                node.Inputs.Clear();
                
                node.Inputs.Add(new PortModel() { IsInput = true, Index = 0, Label = "L" });
                node.Inputs.Add(new PortModel() { IsInput = true, Index = 1, Label = "R" });
                break;
            case NodeType.Compression:
                node.Options.Add(new OptionModel() { Title = "Amount (%)" });
                break;
            case NodeType.Delay:
                node.Options.Add(new OptionModel() { Title = "Time (ms)" });
                break;
            case NodeType.Distortion:
                node.Options.Add(new OptionModel() { Title = "Intensity (%)" });
                break;
            case NodeType.Gain:
                node.Options.Add(new OptionModel() { Title = "Gain (x)" });
                break;
            case NodeType.Gating:
                node.Options.Add(new OptionModel() { Title = "Threshold (%)" });
                break;
            case NodeType.Reverb:
                node.Options.Add(new OptionModel() { Title = "Intensity (%)" });
                break;
        }
        
        EditorVm.Nodes.Add(new NodeControlModel(node));
    }
}