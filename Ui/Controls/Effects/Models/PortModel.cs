using System;

namespace Vice.Ui.Controls.Effects.Models;

public class PortModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public bool IsInput { get; set; }
    public int Index { get; set; }
    public string? Label { get; set; }
}
