using Godot;

namespace Ashenfall.Enemies;

/// <summary>Rest-pose hierarchy only. Reparents generated nodes without editing mesh resources.</summary>
[Tool]
public partial class FallenGuardianPresentation : Node3D
{
    public override void _Ready()
    {
        if (HasNode("PoseRoot")) return;
        var source = GetNode<Node3D>("Source");
        var anatomy = source.GetNode<Node3D>("Anatomy/Visual");
        var details = source.GetNode<Node3D>("CursedDetails");
        var pose = Pivot(this, "PoseRoot", Vector3.Zero);
        var pelvis = Pivot(pose, "PelvisPivot", anatomy.GetNode<Node3D>("Pelvis").Position);
        var torso = Pivot(pelvis, "TorsoPivot", anatomy.GetNode<Node3D>("RibcageAndSpine").Position);
        var head = Pivot(torso, "HeadPivot", anatomy.GetNode<Node3D>("Head").Position);
        Move(anatomy, "Pelvis", pelvis);
        Move(anatomy, "RibcageAndSpine", torso);
        Move(anatomy, "Head", head);
        Move(details, "CursedEyes", head);
        foreach (string side in new[] { "Left", "Right" })
        {
            var armPart = anatomy.GetNode<Node3D>(side + "Arm");
            var arm = Pivot(torso, side + "ArmPivot", armPart.Position);
            armPart.Reparent(arm, true);
            var legPart = anatomy.GetNode<Node3D>(side + "Leg");
            var leg = Pivot(pelvis, side + "LegPivot", legPart.Position);
            legPart.Reparent(leg, true);
            string equipment = side == "Right" ? "Sword" : "Shield";
            var attachment = Pivot(arm, equipment + "Attachment", anatomy.GetNode<Node3D>(equipment).Position);
            Move(anatomy, equipment, attachment);
        }
        Move(details, "CorrodedArmor", torso); // Both shoulders remain one rigid resource.
        Move(details, "TatteredCloth", pelvis);
        Move(anatomy, "LeatherAndRags", torso); // Harness and rear rags remain combined.
    }

    private Node3D Pivot(Node3D parent, string name, Vector3 modelPosition)
    {
        var pivot = new Node3D { Name = name };
        parent.AddChild(pivot);
        pivot.GlobalPosition = ToGlobal(modelPosition);
        return pivot;
    }

    private static void Move(Node parent, string name, Node3D destination) =>
        parent.GetNode<Node3D>(name).Reparent(destination, true);
}
