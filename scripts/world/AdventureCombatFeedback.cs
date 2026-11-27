using Ashenfall.Characters;
using Ashenfall.Combat;
using Ashenfall.Enemies;
using Godot;
using System.Collections.Generic;

namespace Ashenfall.World;

/// <summary>Visual feedback and silent audio cues; never resolves combat.</summary>
public partial class AdventureCombatFeedback : Node3D
{
    [Signal] public delegate void SoundCueRequestedEventHandler(StringName cue, Vector3 position);

    [Export(PropertyHint.Range, "0.05,1,0.01")] public float FlashSeconds { get; set; } = 0.16f;
    [Export(PropertyHint.Range, "0.8,1.2,0.01")] public float NumberSeconds { get; set; } = 1.0f;
    [Export] public Color DealtColor { get; set; } = new(1f, 0.92f, 0.78f);
    [Export] public Color ReceivedColor { get; set; } = new(0.9f, 0.30f, 0.27f);
    [Export] public Color CriticalColor { get; set; } = Colors.Yellow;
    public int FeedbackCount { get; private set; }
    public int AudioCueCount { get; private set; }
    public int ActiveNumberCount => _numbers.Count;

    private sealed class Flash
    {
        public float Remaining;
        public readonly Dictionary<MeshInstance3D, Material> Original = new();
    }

    private readonly Dictionary<Node3D, Flash> _flashes = new();
    private readonly List<Node3D> _expiredFlashes = new();
    private readonly Dictionary<ulong, int> _lastSequence = new();
    private readonly Dictionary<EnemyMeleeDamageController, EnemyMeleeDamageController.DamageAppliedEventHandler> _enemyDamageHandlers = new();
    private readonly Dictionary<EnemyController, EnemyController.AttackExecutedEventHandler> _enemyAttackHandlers = new();
    private readonly List<Label3D> _numbers = new();
    private MeleeDamageController _playerDamage;
    private PlayerMeleeController _playerMelee;
    private HealthComponent _playerHealth;
    private DwarfPlayerPresentation _dwarf;
    private AnimationPlayer _dwarfAnimation;
    private Node3D _playerActor;
    private float _previousStepTime = -1;
    private readonly Dictionary<HealthComponent, HealthComponent.DiedEventHandler> _deathHandlers = new();
    private SphereMesh _sparkMesh;
    private StandardMaterial3D _playerSparkMaterial, _enemySparkMaterial;
    private StandardMaterial3D _playerFlashMaterial, _enemyFlashMaterial;

    public override void _Ready()
    {
        // Sample AI state after its physics transition, before the next render.
        ProcessPhysicsPriority = 2;
        var adventure = GetParent();
        _playerActor = adventure.GetNode<Node3D>("World/FutureActors/Player");
        _playerHealth = _playerActor.GetNode<HealthComponent>("Health");
        _dwarf = adventure.GetNode<DwarfPlayerPresentation>("DwarfPresentation");
        _dwarfAnimation = _dwarf.Presentation.GetNode<AnimationPlayer>("AnimationPlayer");
        _sparkMesh = new SphereMesh { Radius = 0.014f, Height = 0.028f, RadialSegments = 6, Rings = 3 };
        _playerSparkMaterial = ColorMaterial(new Color(0.95f, 0.28f, 0.20f, 1));
        _enemySparkMaterial = ColorMaterial(new Color(0.94f, 0.72f, 0.42f, 1));
        _playerFlashMaterial = ColorMaterial(new Color(0.85f, 0.31f, 0.27f, 0.14f));
        _enemyFlashMaterial = ColorMaterial(new Color(0.84f, 0.69f, 0.48f, 0.14f));

        foreach (var node in adventure.GetChildren())
        {
            if (node is MeleeDamageController playerDamage)
            {
                _playerDamage = playerDamage;
                playerDamage.DamageApplied += OnPlayerImpact;
            }
            if (node is PlayerMeleeController melee)
            {
                _playerMelee = melee;
                melee.AttackStarted += OnPlayerAttackStarted;
            }
            if (node is EnemyMeleeDamageController enemyDamage)
            {
                EnemyMeleeDamageController.DamageAppliedEventHandler handler = (target, amount, sequence) =>
                    OnImpact(enemyDamage.GetInstanceId(), target, amount, sequence, true,
                        enemyDamage.LastImpactCritical);
                _enemyDamageHandlers.Add(enemyDamage, handler);
                enemyDamage.DamageApplied += handler;
            }
            if (node is EnemyController enemy)
            {
                EnemyController.AttackExecutedEventHandler handler = (_, _) => Cue("GuardianSwing", enemy.GlobalPosition);
                _enemyAttackHandlers.Add(enemy, handler);
                enemy.AttackExecuted += handler;
                var health = enemy.GetNode<HealthComponent>("Health");
                HealthComponent.DiedEventHandler deathHandler = () => Cue("GuardianDefeat", enemy.GlobalPosition);
                _deathHandlers.Add(health, deathHandler);
                health.Died += deathHandler;
            }
        }
        HealthComponent.DiedEventHandler playerDeath = () => Cue("PlayerDefeat", _playerActor.GlobalPosition);
        _deathHandlers.Add(_playerHealth, playerDeath);
        _playerHealth.Died += playerDeath;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_playerDamage)) _playerDamage.DamageApplied -= OnPlayerImpact;
        if (GodotObject.IsInstanceValid(_playerMelee)) _playerMelee.AttackStarted -= OnPlayerAttackStarted;
        foreach (var (source, handler) in _enemyDamageHandlers)
            if (GodotObject.IsInstanceValid(source)) source.DamageApplied -= handler;
        foreach (var (enemy, handler) in _enemyAttackHandlers)
            if (GodotObject.IsInstanceValid(enemy)) enemy.AttackExecuted -= handler;
        foreach (var (health, handler) in _deathHandlers)
            if (GodotObject.IsInstanceValid(health)) health.Died -= handler;
        foreach (var actor in new List<Node3D>(_flashes.Keys)) RestoreFlash(actor);
    }

    private static StandardMaterial3D ColorMaterial(Color color) => new()
    {
        AlbedoColor = color,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private void OnPlayerAttackStarted(Node3D _, int __) => Cue("AxeSwing", _playerActor.GlobalPosition);
    private void OnPlayerImpact(Node3D target, int amount, int sequence) =>
        OnImpact(_playerDamage.GetInstanceId(), target, amount, sequence, false,
            _playerDamage.LastImpactCritical);

    private void OnImpact(ulong sourceId, Node3D target, int amount, int sequence,
        bool playerWasHit, bool critical)
    {
        if (!GodotObject.IsInstanceValid(target) || amount <= 0 || sequence <= 0 ||
            (_lastSequence.TryGetValue(sourceId, out int last) && sequence <= last)) return;
        _lastSequence[sourceId] = sequence;
        FeedbackCount++;
        FlashActor(target, playerWasHit);
        SpawnSparks(target, playerWasHit);
        SpawnNumber(target, amount, playerWasHit, critical);
        Cue(playerWasHit ? "PlayerImpact" : "GuardianImpact", target.GlobalPosition);
    }

    private void Cue(StringName cue, Vector3 position)
    {
        AudioCueCount++;
        EmitSignal(SignalName.SoundCueRequested, cue, position);
    }

    private void FlashActor(Node3D actor, bool player)
    {
        if (_flashes.TryGetValue(actor, out var existing))
        {
            existing.Remaining = FlashSeconds;
            return;
        }
        var visual = actor.GetNodeOrNull<Node3D>("Visual");
        if (visual == null) return;
        var flash = new Flash { Remaining = FlashSeconds };
        foreach (var node in visual.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || !mesh.IsVisibleInTree()) continue;
            flash.Original.Add(mesh, mesh.MaterialOverlay);
            mesh.MaterialOverlay = player ? _playerFlashMaterial : _enemyFlashMaterial;
        }
        _flashes.Add(actor, flash);
    }

    private void RestoreFlash(Node3D actor)
    {
        if (!_flashes.TryGetValue(actor, out var flash)) return;
        foreach (var (mesh, material) in flash.Original)
            if (GodotObject.IsInstanceValid(mesh)) mesh.MaterialOverlay = material;
        _flashes.Remove(actor);
    }

    private void SpawnSparks(Node3D actor, bool player)
    {
        var burst = new Node3D { Name = "ImpactSparks" };
        AddChild(burst);
        burst.GlobalPosition = actor.GlobalPosition + Vector3.Up * (player ? 0.85f : 1.15f);
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Tau * i / 5f + (player ? 0.2f : 0.55f);
            var spark = new MeshInstance3D
            {
                Mesh = _sparkMesh,
                MaterialOverride = player ? _playerSparkMaterial : _enemySparkMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            burst.AddChild(spark);
            var drift = new Vector3(Mathf.Cos(angle) * 0.18f, 0.12f + (i % 2) * 0.06f,
                Mathf.Sin(angle) * 0.18f);
            var tween = burst.CreateTween().SetParallel(true);
            tween.TweenProperty(spark, "position", drift, 0.28f);
            tween.TweenProperty(spark, "scale", Vector3.Zero, 0.28f);
        }
        var cleanup = burst.CreateTween();
        cleanup.TweenInterval(0.35f);
        cleanup.TweenCallback(Callable.From(burst.QueueFree));
    }

    private void SpawnNumber(Node3D actor, int amount, bool playerWasHit, bool critical)
    {
        var color = critical ? CriticalColor : playerWasHit ? ReceivedColor : DealtColor;
        var label = new Label3D
        {
            Name = "DamageNumber", Text = $"{amount}",
            FontSize = 60, PixelSize = 0.007f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true, OutlineSize = 7, Modulate = color
        };
        AddChild(label);
        label.GlobalPosition = actor.GlobalPosition + Vector3.Up * (playerWasHit ? 1.72f : 2.62f);
        _numbers.Add(label);
        var tween = label.CreateTween().SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector3(0, 0.34f, 0), NumberSeconds);
        tween.TweenProperty(label, "modulate", new Color(color.R, color.G, color.B, 0), NumberSeconds);
        var cleanup = label.CreateTween();
        cleanup.TweenInterval(NumberSeconds + 0.02f);
        cleanup.TweenCallback(Callable.From(() => { _numbers.Remove(label); label.QueueFree(); }));
    }

    public override void _Process(double delta)
    {
        _expiredFlashes.Clear();
        foreach (var (actor, flash) in _flashes)
        {
            flash.Remaining -= (float)delta;
            if (flash.Remaining <= 0 || !GodotObject.IsInstanceValid(actor)) _expiredFlashes.Add(actor);
        }
        foreach (var actor in _expiredFlashes) RestoreFlash(actor);
        if (!GodotObject.IsInstanceValid(_dwarfAnimation) || _dwarf.Defeated ||
            _dwarfAnimation.CurrentAnimation != "Combat/CombatWalk" || _dwarf.ActualHorizontalSpeed < 0.08f)
        {
            _previousStepTime = -1;
            return;
        }
        float time = (float)_dwarfAnimation.CurrentAnimationPosition;
        if (_previousStepTime >= 0 &&
            (time < _previousStepTime || (_previousStepTime < 0.30f && time >= 0.30f)))
            Cue("PlayerFootstep", _playerActor.GlobalPosition);
        _previousStepTime = time;
    }
}
