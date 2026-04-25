using System;

namespace Vice.Ui.Controls.Effects.Models;

public class ConnectionModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string FromNodeId { get; set; } = string.Empty;
    public string FromPortId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string ToPortId { get; set; } = string.Empty;
}
