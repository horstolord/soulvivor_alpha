using System;

namespace Sandbox.Code.Data;

public enum ModifierType { Flat, Percent }

public class StatModifier
{
	public float Value;
	public ModifierType Type;
	public object Source;      // the buff/item/spell that applied it — needed for removal
	public float? Duration;    // null = permanent (e.g. from equipment)
	public TimeSince TimeSinceApplied;

	public StatModifier( float value, ModifierType type, object source = null )
	{
		Value = value;
		Type = type;
		Source = source;
	}
}

public class Stat
{
	private float _baseValue;
	public float BaseValue
	{
		get => _baseValue;
		set
		{
			_baseValue = value;
			_dirty = true;
		}
	}
	private readonly List<StatModifier> _modifiers = new();
	private float _cachedValue;
	private bool _dirty = true;

	public float Value
	{
		get
		{
			if ( _dirty ) Recalculate();
			return _cachedValue;
		}
	}

	public Stat( float baseValue = 0f )
	{
		BaseValue = baseValue;
	}

	public void AddModifier( StatModifier mod )
	{
		_modifiers.Add( mod );
		_dirty = true;
	}

	public void RemoveModifier( StatModifier modifier )
	{
		if ( _modifiers.Remove( modifier ) )
			_dirty = true;
	}

	public void RemoveModifiersFromSource( object source )
	{
		if ( _modifiers.RemoveAll( m => m.Source == source ) > 0 )
			_dirty = true;
	}

	private void Recalculate()
	{
		float flat = 0, percent = 1f;

		foreach ( var m in _modifiers )
		{
			switch ( m.Type )
			{
				case ModifierType.Flat:
					flat += m.Value;
					break;
				case ModifierType.Percent:
					percent *= (1f + m.Value / 100f);
					break;
			}
		}

		_cachedValue = (BaseValue + flat) * percent;
		_dirty = false;
	}

	public override string ToString()
	{
		return $"{Value:F1}";
	}
}

