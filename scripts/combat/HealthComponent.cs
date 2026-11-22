using Godot;

namespace Ashenfall.Combat;

/// <summary>Model-independent, bounded hit points. Damage is accepted only while alive.</summary>
public partial class HealthComponent : Node
{
    [Signal] public delegate void HealthChangedEventHandler(int current, int maximum);
    [Signal] public delegate void DiedEventHandler();

    private int _maximum = 100;
    private int _current;
    private bool _initialized;

    [Export(PropertyHint.Range, "1,10000,1")]
    public int MaximumHealth
    {
        get => _maximum;
        set
        {
            int next = Mathf.Max(1, value);
            if (_maximum == next) return;
            _maximum = next;
            if (!_initialized) return;
            _current = Mathf.Clamp(_current, 0, _maximum);
            EmitSignal(SignalName.HealthChanged, _current, _maximum);
        }
    }

    public int CurrentHealth => _current;
    public bool IsAlive => _initialized && _current > 0;

    public override void _Ready()
    {
        _current = _maximum;
        _initialized = true;
        EmitSignal(SignalName.HealthChanged, _current, _maximum);
    }

    /// <returns>True only when positive damage changed live health.</returns>
    public bool ApplyDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return false;
        _current = Mathf.Max(0, _current - amount);
        EmitSignal(SignalName.HealthChanged, _current, _maximum);
        if (_current == 0) EmitSignal(SignalName.Died);
        return true;
    }
}
