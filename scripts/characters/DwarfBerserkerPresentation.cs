using Godot;
using System.Collections.Generic;

namespace Ashenfall.Characters;

/// <summary>Visual-only AnimationPlayer adapter. Rigid two-bone constraints preserve the authored meshes.</summary>
public partial class DwarfBerserkerPresentation : Node3D
{
    [Export] public Vector3 BodyOffset { get; set; }
    [Export] public Vector3 TorsoRotation { get; set; }
    [Export] public Vector3 HeadRotation { get; set; }
    [Export] public Vector3 WeaponOffset { get; set; }
    [Export] public Vector3 WeaponRotation { get; set; }
    [Export] public Vector3 BeardRotation { get; set; }
    [Export] public Vector3 LeftFootOffset { get; set; }
    [Export] public Vector3 RightFootOffset { get; set; }
    [Export] public float LeftFootPitch { get; set; }
    [Export] public float RightFootPitch { get; set; }
    [Export(PropertyHint.Range, "0,0.5,0.01")] public double BlendSeconds { get; set; } = 0.20;
    public StringName ActiveClip { get; private set; } = "";
    private Node3D _model, _pelvis, _torso, _head, _beard, _axe;
    private AnimationPlayer _player;
    private Transform3D _pelvisRest, _axeRest;
    private Vector3 _weaponCenter;
    private readonly List<Limb> _arms = new(), _legs = new();

    private sealed class Limb
    {
        public Node3D Upper, Middle, End, Grip;
        public Vector3 Start, Joint, Tip, Pole, GripOffset;
        public float UpperLength, LowerLength;
        public Vector3[] BootVertices;
    }

    private Node3D Find(string name) => (Node3D)_model.FindChild(name, true, false);
    private Vector3 ModelPosition(Node3D node) => _model.ToLocal(node.GlobalPosition);

    public override void _Ready()
    {
        _model = GetNode<Node3D>("Model");
        _player = GetNode<AnimationPlayer>("AnimationPlayer");
        _pelvis = Find("PelvisPivot"); _torso = Find("TorsoPivot");
        _head = Find("HeadPivot"); _beard = Find("Beard"); _axe = Find("BattleAxe");
        _pelvisRest = _pelvis.Transform;
        foreach (string side in new[] { "Left", "Right" })
        {
            var arm = Capture(side + "ShoulderPivot", side + "ElbowPivot", side + "WristPivot");
            arm.Grip = _axe.GetNode<Node3D>(side + "Grip");
            arm.GripOffset = arm.Tip - ModelPosition(arm.Grip);
            _arms.Add(arm);
            var leg = Capture(side + "HipPivot", side + "KneePivot", side + "AnklePivot");
            // Actual boot vertices, including the calf collar: ground compensation uses the real geometry.
            var mesh = Find(side + "Boot").GetNode<MeshInstance3D>("Geometry");
            var vertices = new List<Vector3>();
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                foreach (var vertex in mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    vertices.Add(leg.End.ToLocal(mesh.ToGlobal(vertex)));
            leg.BootVertices = vertices.ToArray();
            _legs.Add(leg);
        }
        // Both hands follow one stable torso attachment. Reparenting retains the exact approved rest transform.
        _weaponCenter = _torso.ToLocal((_arms[0].Grip.GlobalPosition + _arms[1].Grip.GlobalPosition) * 0.5f);
        _axe.Reparent(_torso, true); _axeRest = _axe.Transform;
        _player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        PlayState("Idle");
    }

    private Limb Capture(string upper, string middle, string end)
    {
        var limb = new Limb { Upper = Find(upper), Middle = Find(middle), End = Find(end) };
        limb.Start = ModelPosition(limb.Upper); limb.Joint = ModelPosition(limb.Middle); limb.Tip = ModelPosition(limb.End);
        limb.UpperLength = limb.Start.DistanceTo(limb.Joint); limb.LowerLength = limb.Joint.DistanceTo(limb.Tip);
        var axis = (limb.Tip - limb.Start).Normalized();
        limb.Pole = (limb.Joint - limb.Start - axis * (limb.Joint - limb.Start).Dot(axis)).Normalized();
        return limb;
    }

    public void PlayState(string state)
    {
        if ((state != "Idle" && state != "Walk") || ActiveClip == state) return;
        ActiveClip = state; _player.Play(state, BlendSeconds); _player.Advance(0); ApplyPose();
    }

    public override void _Process(double delta)
    {
        // Explicit ordering: sample/blend authoring controls, then solve constraints in this same frame.
        _player.Advance(delta); ApplyPose();
    }

    /// <summary>Deterministic validation/screenshot sampling, with no active blend.</summary>
    public void SamplePose(string state, double seconds)
    {
        if (state != "Idle" && state != "Walk") return;
        _player.Stop(); ActiveClip = state; _player.Play(state, 0); _player.Seek(seconds, true); ApplyPose();
    }

    public void ApplyPose()
    {
        _pelvis.Transform = new Transform3D(_pelvisRest.Basis, _pelvisRest.Origin + BodyOffset);
        _torso.Rotation = TorsoRotation; _head.Rotation = HeadRotation; _beard.Rotation = BeardRotation;
        _axe.Transform = new Transform3D(Basis.FromEuler(WeaponRotation), _weaponCenter + WeaponOffset)
            * new Transform3D(Basis.Identity, -_weaponCenter) * _axeRest;
        var weaponBasis = _model.GlobalBasis.Inverse() * _axe.GlobalBasis;
        var torsoBasis = _model.GlobalBasis.Inverse() * _torso.GlobalBasis;
        foreach (var arm in _arms)
            Solve(arm, ModelPosition(arm.Grip) + weaponBasis * arm.GripOffset, torsoBasis * arm.Pole, weaponBasis);
        for (int i = 0; i < _legs.Count; i++)
        {
            var leg = _legs[i];
            var offset = i == 0 ? LeftFootOffset : RightFootOffset;
            var footBasis = new Basis(Vector3.Right, i == 0 ? LeftFootPitch : RightFootPitch);
            float bottom = float.PositiveInfinity;
            foreach (var vertex in leg.BootVertices) bottom = Mathf.Min(bottom, (footBasis * vertex).Y);
            // Offset.Y is sole clearance; support soles stay at zero through torso motion and ankle roll.
            var target = new Vector3(leg.Tip.X + offset.X, -bottom + offset.Y, leg.Tip.Z + offset.Z);
            Solve(leg, target, leg.Pole, footBasis);
        }
    }

    private void Solve(Limb limb, Vector3 target, Vector3 pole, Basis endBasis)
    {
        var start = ModelPosition(limb.Upper);
        var delta = target - start;
        float distance = Mathf.Clamp(delta.Length(), Mathf.Abs(limb.UpperLength - limb.LowerLength) + 0.00001f,
            limb.UpperLength + limb.LowerLength - 0.00001f);
        var axis = delta.Normalized();
        target = start + axis * distance;
        float along = (limb.UpperLength * limb.UpperLength - limb.LowerLength * limb.LowerLength + distance * distance) / (2 * distance);
        var bend = (pole - axis * pole.Dot(axis)).Normalized();
        var joint = start + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, limb.UpperLength * limb.UpperLength - along * along));
        // Rigid rotations of the original segment directions, never mesh regeneration, translation or scaling.
        var upperBasis = Align((limb.Joint - limb.Start).Normalized(), (joint - start).Normalized());
        var lowerBasis = Align((limb.Tip - limb.Joint).Normalized(), (target - joint).Normalized());
        limb.Upper.GlobalBasis = _model.GlobalBasis * upperBasis;
        limb.Middle.GlobalBasis = _model.GlobalBasis * lowerBasis;
        limb.End.GlobalBasis = _model.GlobalBasis * endBasis;
    }

    private static Basis Align(Vector3 from, Vector3 to)
    {
        // Cross/dot construction stays precise for the tiny idle rotations (acos loses precision near 1).
        // Authored targets remain far from a 180-degree reversal.
        var cross = from.Cross(to);
        return new Basis(new Quaternion(cross.X, cross.Y, cross.Z, 1 + from.Dot(to)).Normalized());
    }
}
