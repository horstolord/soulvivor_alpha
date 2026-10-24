using System;
using System.Collections.Generic;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Owns the active statuses on one actor (player or enemy, same rules). Created by Actor.OnStart.
/// Each proc is its own StatusInstance with its own timer; stat debuffs are tracked StatModifiers
/// (same removal pattern as BuffComponent), damage over time is summed per type into one tick.
/// </summary>
public sealed class StatusControl : Component
{
	/// <summary>Hook point for the per-actor proc event bus (boons / elite affixes react to these).</summary>
	public event Action<StatusInstance> StatusApplied;
	public event Action<StatusInstance> StatusExpired;

	private readonly List<StatusInstance> _active = new();
	private readonly Dictionary<StatusType, float> _tickTimers = new();
	private Actor _actor;

	/// <summary>Seconds an enemy is immune to new crowd control after one ends (no chain-stun-locking).</summary>
	[Property] public float CcImmunityDuration { get; set; } = 3f;
	private float _ccImmuneUntil;

	public int GetStacks( StatusType type )
	{
		int count = 0;
		foreach ( var s in _active )
			if ( s.Def.Type == type ) count++;
		return count;
	}

	public bool Has( StatusType type ) => GetStacks( type ) > 0;

	/// <summary>
	/// Adds one stack. hitDamage is the (post-crit) health damage of the causing hit and scales DoT strength.
	/// potency/durationScale come from the attacker's EffectPotency/EffectDuration (1 = 100%).
	/// </summary>
	public bool Apply( StatusDef def, GameObject source, float hitDamage, float potency = 1f, float durationScale = 1f )
	{
		if ( def == null || !CacheActor() ) return false;

		var sheet = _actor.StatSheet;
		if ( sheet?.MaxHealth == null ) return false; // stats not initialized yet
		if ( _actor.StateComp?.CurrentState == ActorStateType.Dead ) return false;

		bool enemy = _actor is Enemy;
		if ( def.EnemyOnly && !enemy ) return false;
		if ( def.IsCrowdControl && enemy && Time.Now < _ccImmuneUntil ) return false;

		// Stack cap per type: a new proc replaces the one closest to expiring.
		int count = 0;
		StatusInstance oldest = null;
		foreach ( var s in _active )
		{
			if ( s.Def.Type != def.Type ) continue;
			count++;
			if ( oldest == null || s.ExpiresAt < oldest.ExpiresAt ) oldest = s;
		}
		if ( count >= def.MaxStacks && oldest != null )
			Remove( oldest );

		float duration = MathF.Max( 0.05f, def.Duration * durationScale );
		var instance = new StatusInstance
		{
			Def = def,
			Source = source,
			ExpiresAt = Time.Now + duration,
			HitDamage = hitDamage,
			Potency = potency,
			DamagePerSecond = def.DamageFraction > 0f
				? hitDamage * def.DamageFraction * potency / MathF.Max( 0.05f, def.Duration )
				: 0f
		};

		var mods = !enemy && def.PlayerModifiers != null ? def.PlayerModifiers : def.Modifiers;
		foreach ( var m in mods )
		{
			if ( m.StatName is "MaxHealth" or "MaxStamina" or "MaxEnergy" or "MaxPoise" ) instance.AffectsPools = true;
			var stat = sheet.GetStat( m.StatName );
			if ( stat == null )
			{
				Log.Warning( $"[Status] '{def.DisplayName}' references unknown stat '{m.StatName}'" );
				continue;
			}

			var modifier = new StatModifier( m.Value * potency, m.Type, source: instance );
			stat.AddModifier( modifier );
			instance.AppliedModifiers.Add( (stat, modifier) );
		}

		_active.Add( instance );
		if ( instance.AffectsPools ) sheet.RecalculateDerivedStats();

		if ( def.PoiseDrainFraction > 0f && sheet.MaxPoise != null )
		{
			sheet.CurrentPoise = MathF.Max( 0f, sheet.CurrentPoise - sheet.MaxPoise.Value * def.PoiseDrainFraction * potency );
			sheet.ResetPoiseTimer(); // otherwise the poise refill timer undoes the drain
			if ( sheet.CurrentPoise <= 0f ) TryFreezeOnPoiseBreak( source ); // chill drained the last poise
		}

		def.Behavior?.OnApplied( _actor, instance );
		StatusApplied?.Invoke( instance );
		return true;
	}

	/// <summary>
	/// Called by Actor when poise hits zero. While chilled (and not immune / not the player) the break becomes
	/// Frozen instead of a short stagger: consumes the Chill stacks and refills poise. Returns true if it froze.
	/// </summary>
	public bool TryFreezeOnPoiseBreak( GameObject source = null )
	{
		if ( !Has( StatusType.Chill ) || !Apply( StatusLibrary.Frozen, source, 0f ) ) return false;

		for ( int i = _active.Count - 1; i >= 0; i-- )
			if ( _active[i].Def.Type == StatusType.Chill ) Remove( _active[i] );

		var sheet = _actor.StatSheet;
		sheet.CurrentPoise = sheet.MaxPoise.Value;
		sheet.ResetPoiseTimer();
		return true;
	}

	public void ClearAll()
	{
		for ( int i = _active.Count - 1; i >= 0; i-- )
			Remove( _active[i] );
	}

	protected override void OnUpdate()
	{
		if ( !CacheActor() || _active.Count == 0 ) return;

		// Death clears everything (and covers the player, who isn't destroyed on death).
		if ( _actor.StateComp?.CurrentState == ActorStateType.Dead )
		{
			ClearAll();
			return;
		}

		float now = Time.Now;
		for ( int i = _active.Count - 1; i >= 0; i-- )
		{
			if ( now >= _active[i].ExpiresAt )
				Remove( _active[i] );
		}

		TickDamageOverTime( Time.Delta );
	}

	private void TickDamageOverTime( float dt )
	{
		if ( _active.Count == 0 ) return;

		foreach ( var def in StatusLibrary.All )
		{
			if ( def.TickInterval <= 0f ) continue;

			float dps = 0f;
			GameObject source = null;
			foreach ( var s in _active )
			{
				if ( s.Def.Type != def.Type ) continue;
				dps += s.DamagePerSecond;
				if ( s.Source.IsValid() ) source = s.Source;
			}

			if ( dps <= 0f )
			{
				_tickTimers[def.Type] = def.TickInterval; // idle: next proc starts a fresh interval
				continue;
			}

			float timer = (_tickTimers.TryGetValue( def.Type, out var t ) ? t : def.TickInterval) - dt;
			if ( timer > 0f )
			{
				_tickTimers[def.Type] = timer;
				continue;
			}
			_tickTimers[def.Type] = timer + def.TickInterval;

			// One merged tick per type, so ten bleed stacks are one damage number, not ten.
			_actor.ApplyDamage( new DamageProfileDef
			{
				HealthDamage = dps * def.TickInterval,
				Tags = def.DamageTags,
				IsDot = true
			}, source );

			if ( _actor.StateComp?.CurrentState == ActorStateType.Dead ) return;
		}
	}

	private void Remove( StatusInstance instance )
	{
		foreach ( var (stat, modifier) in instance.AppliedModifiers )
			stat.RemoveModifier( modifier );

		_active.Remove( instance );

		if ( instance.AffectsPools )
			_actor?.StatSheet?.RecalculateDerivedStats();

		if ( instance.Def.IsCrowdControl && _actor is Enemy )
			_ccImmuneUntil = Time.Now + CcImmunityDuration;

		instance.Def.Behavior?.OnExpired( _actor, instance );
		StatusExpired?.Invoke( instance );
	}

	private bool CacheActor()
	{
		_actor ??= Components.GetInAncestorsOrSelf<Actor>();
		return _actor != null;
	}
}

/// <summary>
/// The status roll, parallel to the crit roll: called once per landed hit from HitResolver.
/// Every status whose RequiredTag is on the hit rolls independently, so a hit can proc none, either or both.
/// Chance is in percent; 250 = two guaranteed procs plus a 50% roll for a third.
/// </summary>
public static class StatusRoller
{
	public static void Roll( Actor attacker, Actor target, DamageProfileDef damage, GameObject source )
	{
		if ( damage == null || damage.IsDot || damage.Tags == null ) return;
		if ( attacker?.StatSheet?.StatusChance == null || target?.Statuses == null ) return;

		var sheet = attacker.StatSheet;
		float baseChance = sheet.StatusChance.Value * MathF.Max( 0f, damage.StatusChanceMultiplier );
		if ( baseChance <= 0f ) return;

		float resistScale = MathF.Max( 0f, 1f - (target.StatSheet?.StatusResistance?.Value ?? 0f) / 100f );
		float potency = (sheet.EffectPotency?.Value ?? 100f) / 100f;
		float durationScale = (sheet.EffectDuration?.Value ?? 100f) / 100f;

		foreach ( var def in StatusLibrary.All )
		{
			if ( !damage.Tags.Contains( def.RequiredTag ) ) continue;

			float chance = baseChance * def.ChanceScale * resistScale;
			int procs = (int)(chance / 100f);
			if ( Random.Shared.NextSingle() * 100f < chance - procs * 100f ) procs++;

			for ( int i = 0; i < procs; i++ )
				target.Statuses.Apply( def, source, damage.HealthDamage, potency, durationScale );
		}
	}
}
