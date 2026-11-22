using Godot;

namespace Ashenfall.Characters;

/// <summary>Keeps a visual-only defeat pose's body contact on the actor's ground plane.</summary>
public static class VisualGrounding
{
    public static void Settle(Node3D visual, Node3D actor, float clearance = 0.012f)
    {
        float lowest = float.PositiveInfinity;
        foreach (var node in visual.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || !mesh.IsVisibleInTree() || ExcludedEquipment(mesh)) continue;
            var box = mesh.GetAabb();
            for (int corner = 0; corner < 8; corner++)
            {
                var local = box.Position + new Vector3(
                    (corner & 1) == 0 ? 0 : box.Size.X,
                    (corner & 2) == 0 ? 0 : box.Size.Y,
                    (corner & 4) == 0 ? 0 : box.Size.Z);
                lowest = Mathf.Min(lowest, mesh.ToGlobal(local).Y);
            }
        }
        if (!float.IsFinite(lowest)) return;
        visual.GlobalPosition += Vector3.Up * (actor.GlobalPosition.Y + clearance - lowest);
    }

    private static bool ExcludedEquipment(Node node)
    {
        for (Node current = node; current != null; current = current.GetParent())
        {
            string name = current.Name.ToString();
            if (name.Contains("BattleAxe") || name.Contains("Sword") || name.Contains("Shield")) return true;
        }
        return false;
    }
}
