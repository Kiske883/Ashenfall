using Ashenfall.Combat;
using Godot;

namespace Ashenfall.UI;

/// <summary>One billboard quad per enemy; its persistent texture contains both bar regions.</summary>
public partial class WorldHealthBar : Node3D
{
    private const int TextureWidth = 128;
    private const int TextureHeight = 16;

    [Export] public HealthComponent Health { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")] public float HealthyThreshold { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float CriticalThreshold { get; set; } = 0.25f;
    [Export] public Color HealthyColor { get; set; } = new(0.26f, 0.62f, 0.34f);
    [Export] public Color WoundedColor { get; set; } = new(0.88f, 0.46f, 0.12f);
    [Export] public Color CriticalColor { get; set; } = new(0.68f, 0.12f, 0.1f);
    [Export] public Color BackgroundColor { get; set; } = new(0.06f, 0.055f, 0.05f);
    public float DisplayedFraction { get; private set; }
    public Color DisplayedColor { get; private set; }

    private StandardMaterial3D _material;
    private Image _pixels;
    private ImageTexture _texture;

    public override void _Ready()
    {
        var bar = GetNode<MeshInstance3D>("Bar");
        if (Health == null || bar.MaterialOverride is not StandardMaterial3D template)
        {
            GD.PushError("World health bar requires health and a standard material on Bar.");
            return;
        }
        // Duplicate once per enemy; no mutable material is shared between instances.
        _material = (StandardMaterial3D)template.Duplicate();
        _pixels = Image.CreateEmpty(TextureWidth, TextureHeight, false, Image.Format.Rgba8);
        _pixels.Fill(BackgroundColor);
        _texture = ImageTexture.CreateFromImage(_pixels);
        _material.AlbedoColor = Colors.White;
        _material.AlbedoTexture = _texture;
        bar.MaterialOverride = _material;
        Health.HealthChanged += OnHealthChanged;
        OnHealthChanged(Health.CurrentHealth, Health.MaximumHealth);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Health)) Health.HealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int current, int maximum)
    {
        // HealthChanged is emitted before Died, so the bar disappears on the lethal hit.
        // A newly instanced living Guardian sets this back to true on restart.
        Visible = current > 0;
        float fraction = Mathf.Clamp((float)current / Mathf.Max(1, maximum), 0, 1);
        float high = Mathf.Max(HealthyThreshold, CriticalThreshold);
        float low = Mathf.Min(HealthyThreshold, CriticalThreshold);
        Color fill = fraction > high ? HealthyColor :
            fraction > low ? WoundedColor : CriticalColor;
        DisplayedFraction = fraction;
        DisplayedColor = fill;

        // The quad, its transform, material and texture stay fixed. Only pixels change.
        _pixels.Fill(BackgroundColor);
        int filled = Mathf.Clamp(Mathf.RoundToInt(fraction * TextureWidth), 0, TextureWidth);
        if (filled > 0) _pixels.FillRect(new Rect2I(0, 0, filled, TextureHeight), fill);
        _texture.Update(_pixels);
    }
}
