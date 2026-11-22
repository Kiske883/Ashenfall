#nullable enable
using System;
using System.Collections.Generic;
using Ashenfall.Characters;
using Ashenfall.Combat;
using Ashenfall.Enemies;
using Godot;

namespace Ashenfall.Catalog;

/// <summary>Waits for one validated snapshot and configures a fresh adventure before actor _Ready.</summary>
public partial class CatalogAdventureBootstrap : Node
{
    [Export] public PackedScene? AdventureScene { get; set; }
    [Export] public GuardianCatalogOverride? RoadOverride { get; set; }
    [Export] public GuardianCatalogOverride? WestOverride { get; set; }
    [Export] public GuardianCatalogOverride? EastOverride { get; set; }
    public CatalogSnapshot? PinnedSnapshot { get; private set; }
    public AdventureGameplayStats? EffectiveStats { get; private set; }
    public string PinnedVersionKey => PinnedSnapshot?.VersionKey ?? "";
    public string PinnedContentSha256 => PinnedSnapshot?.ContentSha256 ?? "";
    public bool AdventureStarted => _adventure is not null;

    private CatalogRuntime? _runtime;
    private Node? _adventure;
    private CanvasLayer? _loadingLayer;
    private Label? _message;
    private Button? _retry;

    public override void _Ready()
    {
        _runtime = GetNodeOrNull<CatalogRuntime>("/root/CatalogRuntime");
        var layer = new CanvasLayer { Name = "CatalogLoading" };
        AddChild(layer);
        _loadingLayer = layer;
        var center = new CenterContainer { Name = "Center" };
        layer.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { Name = "StatusPanel" };
        panel.CustomMinimumSize = new Vector2(430, 145);
        center.AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);
        _message = new Label { Text = "Loading validated gameplay catalog...",
            HorizontalAlignment = HorizontalAlignment.Center };
        box.AddChild(_message);
        _retry = new Button { Text = "Retry catalog load", Disabled = true };
        _retry.Pressed += OnRetry;
        box.AddChild(_retry);
        if (_runtime is null || AdventureScene is null)
        {
            ShowError("Catalog runtime or adventure scene is missing.");
            SetProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        if (_adventure is not null || _runtime is null) return;
        var candidate = _runtime.ActiveSnapshot;
        if (candidate is not null)
        {
            try { StartAdventure(candidate); }
            catch (Exception error) when (error is CatalogLoadException or InvalidOperationException)
            { ShowError(error.Message); SetProcess(false); }
        }
        else if (_runtime.State == CatalogLoadState.Failed)
            ShowError(_runtime.LastError ?? "Catalog loading failed.");
    }

    private void OnRetry()
    {
        if (_runtime is null || _adventure is not null) return;
        _retry!.Disabled = true;
        _message!.Text = "Retrying validated gameplay catalog...";
        _ = _runtime.ReloadAsync();
        SetProcess(true);
    }

    private void ShowError(string reason)
    {
        if (_message is not null) _message.Text = "Adventure unavailable: " + reason;
        if (_retry is not null) _retry.Disabled = _runtime is null;
    }

    private void StartAdventure(CatalogSnapshot snapshot)
    {
        var stats = GameplayStatsResolver.Resolve(snapshot);
        var adventure = AdventureScene!.Instantiate<Node3D>();
        try { Apply(adventure, stats); }
        catch { adventure.Free(); throw; }
        // Publication to this session happens only after every actor was configured.
        PinnedSnapshot = snapshot;
        EffectiveStats = stats;
        AddChild(adventure);
        _adventure = adventure;
        if (_loadingLayer is not null) _loadingLayer.Visible = false;
        SetProcess(false);
        GD.Print($"Catalog adventure started: version={stats.VersionKey}, hash={stats.ContentSha256}.");
    }

    private void Apply(Node3D adventure, AdventureGameplayStats stats)
    {
        var dwarf = stats.Dwarf;
        var player = adventure.GetNode<PlayerController>("World/FutureActors/Player");
        player.MovementSpeed = dwarf.MoveSpeed;
        adventure.GetNode<HealthComponent>("World/FutureActors/Player/Health").MaximumHealth = dwarf.MaximumHealth;
        var presentation = adventure.GetNode<DwarfPlayerPresentation>("DwarfPresentation");
        presentation.MovementSpeedOverride = 0; // Avoid the older scene-specific 2.5 default winning in _Ready.
        var melee = adventure.GetNode<PlayerMeleeController>("PlayerMelee");
        melee.MeleeRange = dwarf.MeleeRange;
        melee.AttackDuration = dwarf.AttackDuration;
        melee.ImpactFraction = dwarf.ImpactFraction;
        adventure.GetNode<MeleeDamageController>("MeleeDamage").Damage = dwarf.FixedDamage;

        var configured = new HashSet<EnemyController>();
        foreach (var setting in new[] { RoadOverride, WestOverride, EastOverride })
        {
            if (setting is null || setting.GuardianPath is null || setting.GuardianPath.IsEmpty)
                throw new CatalogLoadException("Adventure Guardian override path is missing.");
            var actor = adventure.GetNodeOrNull<EnemyController>(setting.GuardianPath);
            if (actor is null || !configured.Add(actor))
                throw new CatalogLoadException("Adventure Guardian override path is invalid or duplicated.");
            ApplyGuardian(actor, stats.Guardian, setting);
        }
        if (configured.Count != 3 || !configured.Contains(adventure.GetNode<EnemyController>("FallenGuardian")) ||
            !configured.Contains(adventure.GetNode<EnemyController>("GuardianWest")) ||
            !configured.Contains(adventure.GetNode<EnemyController>("GuardianEast")))
            throw new CatalogLoadException("All three first-adventure Guardians require explicit configuration entries.");
        adventure.GetNode<EnemyMeleeDamageController>("EnemyMeleeDamage").Damage = stats.Guardian.FixedDamage;
        adventure.GetNode<EnemyMeleeDamageController>("EnemyMeleeDamage").ImpactDelay = stats.Guardian.ImpactDelay;
        adventure.GetNode<EnemyMeleeDamageController>("WestDamage").Damage = stats.Guardian.FixedDamage;
        adventure.GetNode<EnemyMeleeDamageController>("WestDamage").ImpactDelay = stats.Guardian.ImpactDelay;
        adventure.GetNode<EnemyMeleeDamageController>("EastDamage").Damage = stats.Guardian.FixedDamage;
        adventure.GetNode<EnemyMeleeDamageController>("EastDamage").ImpactDelay = stats.Guardian.ImpactDelay;
    }

    private static void ApplyGuardian(EnemyController actor, GuardianGameplayStats stats,
        GuardianCatalogOverride setting)
    {
        float windup = setting.OverrideWindup ? setting.WindupSeconds : stats.Windup;
        float detection = setting.OverrideDetectionRadius ? setting.DetectionRadius : stats.DetectionRadius;
        float disengagement = setting.OverrideDisengagementRadius
            ? setting.DisengagementRadius : stats.DisengagementRadius;
        float leash = setting.OverrideMaximumDistanceFromSpawn
            ? setting.MaximumDistanceFromSpawn : stats.MaximumDistanceFromSpawn;
        if (windup < 0.05f || detection <= 0 || disengagement <= detection || leash <= 0)
            throw new CatalogLoadException("Adventure Guardian override is invalid.");
        actor.GetNode<HealthComponent>("Health").MaximumHealth = stats.MaximumHealth;
        actor.AttackRange = stats.AttackRange;
        actor.AttackWindupDuration = windup;
        actor.AttackRecoveryDuration = stats.Recovery;
        actor.MovementSpeed = stats.ChaseSpeed;
        actor.ReturnSpeed = stats.ReturnSpeed;
        actor.DetectionRadius = detection;
        actor.DisengagementRadius = disengagement;
        actor.MaximumDistanceFromSpawn = leash;
        actor.StoppingDistance = stats.StoppingDistance;
        actor.ResumeChaseDistance = stats.ResumeChaseDistance;
    }
}
