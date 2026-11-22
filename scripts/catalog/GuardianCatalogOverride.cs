using Godot;

namespace Ashenfall.Catalog;

/// <summary>Explicit per-instance adventure tuning. An unset flag inherits the catalog value.</summary>
[GlobalClass]
public partial class GuardianCatalogOverride : Resource
{
    [Export] public NodePath GuardianPath { get; set; }
    [Export] public bool OverrideWindup { get; set; }
    [Export] public float WindupSeconds { get; set; }
    [Export] public bool OverrideDetectionRadius { get; set; }
    [Export] public float DetectionRadius { get; set; }
    [Export] public bool OverrideDisengagementRadius { get; set; }
    [Export] public float DisengagementRadius { get; set; }
    [Export] public bool OverrideMaximumDistanceFromSpawn { get; set; }
    [Export] public float MaximumDistanceFromSpawn { get; set; }
}
