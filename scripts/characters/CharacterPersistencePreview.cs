#nullable enable
using System;
using HttpClient = System.Net.Http.HttpClient;
using System.Threading;
using Godot;
using Ashenfall.Combat;

namespace Ashenfall.Characters;

/// <summary>Starts a fresh encounter only after accepting a character snapshot.</summary>
public partial class CharacterPersistencePreview : Node
{
    [Export] public PackedScene? AdventureScene { get; set; }
    public PersistedGameplaySnapshot? PinnedSnapshot { get; private set; }
    private readonly HttpClient _http = PersistedCharacterClient.CreateHttpClient();
    private readonly CancellationTokenSource _lifetime = new();
    private Label? _status;
    private Button? _retry;
    private bool _loading;

    public override void _Ready()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        _status = new Label { Text = "Loading authoritative character snapshot...", Position = new Vector2(20, 20), MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_status);
        _retry = new Button { Text = "Retry", Position = new Vector2(20, 110), Visible = false };
        _retry.Pressed += () => { if (!_loading && PinnedSnapshot is null) _ = LoadAsync(); };
        layer.AddChild(_retry);
        _ = LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        _loading = true;
        if (_retry is not null) _retry.Visible = false;
        if (_status is not null) _status.Text = "Loading authoritative character snapshot...";
        try
        {
            if (!OS.IsDebugBuild())
                throw new InvalidOperationException("Character preview is available in development builds only.");
            string? url = System.Environment.GetEnvironmentVariable("ASHENFALL_CHARACTER_API_URL");
            string? token = System.Environment.GetEnvironmentVariable("ASHENFALL_CHARACTER_DEV_TOKEN");
            string? id = System.Environment.GetEnvironmentVariable("ASHENFALL_CHARACTER_ID");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri) || !Guid.TryParse(id, out var expected) || string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Set the local character API URL, character ID and development token.");
            var snapshot = await new PersistedCharacterClient(_http).LoadSnapshotAsync(baseUri, token, expected, _lifetime.Token);
            if (!IsInsideTree()) return;
            if (AdventureScene is null) throw new InvalidOperationException("Encounter scene is missing.");
            var adventure = AdventureScene.Instantiate<Node>();
            try
            {
                var health = adventure.GetNodeOrNull<HealthComponent>("World/FutureActors/Player/Health") ?? throw new InvalidOperationException("Player health is missing.");
                var damage = adventure.GetNodeOrNull<MeleeDamageController>("MeleeDamage") ?? throw new InvalidOperationException("Player damage is missing.");
                health.MaximumHealth = snapshot.EffectiveStats.MaxHealth;
                damage.Damage = snapshot.EffectiveStats.FixedDamage;
                damage.UseFixedDamageOnly = true;
                if (snapshot.SchemaVersion == 2)
                    damage.ConfigureAuthoritativeDamage(snapshot.EffectiveStats.MinDamage!.Value, snapshot.EffectiveStats.MaxDamage!.Value,
                        snapshot.EffectiveStats.CriticalChancePercent!.Value, snapshot.EffectiveStats.CriticalDamageBonusPercent!.Value);
            }
            catch { adventure.Free(); throw; }
            PinnedSnapshot = snapshot;
            AddChild(adventure);
            if (_status is not null) _status.Text = $"Character: {snapshot.CharacterId}  |  Revision: {snapshot.CharacterRevision}\nLevel: {snapshot.EffectiveStats.Level}  |  Max HP: {snapshot.EffectiveStats.MaxHealth}  |  Fixed damage: {snapshot.EffectiveStats.FixedDamage}\nEncounter snapshot pinned";
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException e)
        {
            if (IsInsideTree() && _status is not null) { _status.Text = "Encounter unavailable: " + e.Message; if (_retry is not null) _retry.Visible = true; }
        }
        finally { _loading = false; }
    }

    public override void _ExitTree() { _lifetime.Cancel(); _http.Dispose(); _lifetime.Dispose(); }
}
