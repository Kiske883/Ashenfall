using Godot;

namespace Ashenfall.Art;

/// <summary>Guard-only sword binding. The original model and all mesh resources remain unchanged.</summary>
[Tool]
public partial class FallenGuardianGuardGrip : Node3D
{
    public override void _Ready()
    {
        var model = GetNode<Node3D>("Presentation");
        var wrist = model.GetNode<Node3D>("PoseRoot/PelvisPivot/TorsoPivot/RightShoulderPivot/RightElbowPivot/RightWristPivot");
        var attachment = wrist.GetNode<Node3D>("SwordAttachment");
        // Original recipe coordinates. Four curled fingers are distributed across X.
        var pommel = new Vector3(0.425f, 0.966f, -0.21f);
        var guard = new Vector3(0.465f, 0.79f, -0.26f);
        var swordPivot = new Vector3(0.438f, 0.91f, -0.23f);
        var handleCenter = (pommel + guard) * 0.5f - swordPivot;
        var handGrip = new Vector3(0.432f, 0.86f, -0.244f);
        var handGripLocal = wrist.ToLocal(model.ToGlobal(handGrip));
        var binding = new Basis(new Quaternion((guard - pommel).Normalized(), Vector3.Right));
        // Rotate ABOUT the handle center, then place that center in the curled fingers.
        attachment.Transform = new Transform3D(binding, handGripLocal - binding * handleCenter);
    }
}
