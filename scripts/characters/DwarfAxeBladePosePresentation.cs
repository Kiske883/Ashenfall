using Godot;

namespace Ashenfall.Characters;

/// <summary>
/// Isolated static blade-orientation experiment. The cylindrical handle and
/// cutting head roll together around the shaft; grip markers do not move.
/// </summary>
public partial class DwarfAxeBladePosePresentation : DwarfAxeAttackPresentation
{
    // The approved axe generator places the shaft from butt to end in XY.
    // Its cutting edge extends along (shaft.Y, -shaft.X, 0). By the right-hand
    // rule, +90 degrees about the shaft maps that edge exactly to model -Z.
    private static readonly Vector3 ShaftAxis = (new Vector3(0.46f, 1.50f, -0.43f)
        - new Vector3(-0.46f, 0.45f, -0.43f)).Normalized();
    private MeshInstance3D _geometry;
    private Transform3D _geometryRest;

    public override void _Ready()
    {
        base._Ready();
        var model = GetNode<Node3D>("Model");
        var axe = (Node3D)model.FindChild("BattleAxe", true, false);
        _geometry = axe.GetNode<MeshInstance3D>("Geometry");
        _geometryRest = _geometry.Transform;
        // Geometry coordinates are relative to the butt, so the shaft passes
        // through local zero. Preserve the approved mesh and material objects.
        SetBladeRollDegrees(90);
    }

    /// <summary>Rolls only the axe mesh around its cylindrical handle; grip markers stay fixed.</summary>
    public void SetBladeRollDegrees(float degrees)
    {
        _geometry.Transform = new Transform3D(new Basis(ShaftAxis, Mathf.DegToRad(degrees)), Vector3.Zero)
            * _geometryRest;
    }
}
