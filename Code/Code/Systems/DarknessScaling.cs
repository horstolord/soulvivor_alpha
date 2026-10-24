using System;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

/// <summary>
/// Turns FieldExposure.Darkness into stat modifiers: tankier (DamageTaken) and faster (MoveSpeed)
/// in the dark. Opt-in per actor — Enemy adds it in OnStart, the player never does. Tune per prefab.
/// Both are Percent modifiers, so they stack multiplicatively with items and buffs.
/// Scaling is linear in darkness: at darkness d the modifiers are -MaxDamageReduction * d and
/// +MaxSpeedBonus * d percent.
/// </summary>
public sealed class DarknessScaling : Component
{
	/// <summary>Incoming health damage reduction at full darkness, in percent.</summary>
	[Property, Range( 0f, 100f )] public float MaxDamageReduction { get; set; } = 75f;

	/// <summary>MoveSpeed bonus at full darkness, in percent.</summary>
	[Property] public float MaxSpeedBonus { get; set; } = 50f;

	private Actor _actor;
	private FieldExposure _exposure;

	// The Stat instances we last modified. StatSheet.InitializeFromRegistry replaces them (dropping
	// all modifiers), so a reference mismatch tells us to re-apply.
	private Stat _damageStat, _speedStat;
	private StatModifier _damageMod, _speedMod;
	private float _appliedDarkness = -1f;

	protected override void OnStart()
	{
		_actor = Components.GetInAncestorsOrSelf<Actor>();
		_exposure = _actor?.Exposure ?? _actor?.Components.GetOrCreate<FieldExposure>();
	}

	protected override void OnUpdate()
	{
		var sheet = _actor?.StatSheet;
		// Stats are null until the actor's async init has run.
		if ( _exposure == null || sheet?.MoveSpeed == null || sheet.DamageTaken == null ) return;

		float darkness = _exposure.Darkness;
		if ( darkness == _appliedDarkness && sheet.MoveSpeed == _speedStat && sheet.DamageTaken == _damageStat )
			return;

		_appliedDarkness = darkness;
		Swap( sheet.DamageTaken, ref _damageStat, ref _damageMod, -MaxDamageReduction * darkness );
		Swap( sheet.MoveSpeed, ref _speedStat, ref _speedMod, MaxSpeedBonus * darkness );
	}

	protected override void OnDestroy()
	{
		_damageStat?.RemoveModifier( _damageMod );
		_speedStat?.RemoveModifier( _speedMod );
	}

	private void Swap( Stat stat, ref Stat applied, ref StatModifier mod, float percent )
	{
		if ( mod != null ) applied?.RemoveModifier( mod );
		mod = null;
		applied = stat;

		if ( MathF.Abs( percent ) < 0.001f ) return;
		mod = new StatModifier( percent, ModifierType.Percent, source: this );
		stat.AddModifier( mod );
	}
}
