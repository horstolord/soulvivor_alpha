using System;
using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

/// <summary>Stat debuffs/DoTs live in StatusDef data; these are the statuses that need real code. Placeholder numbers.</summary>

public sealed class FrozenBehavior : IStatusBehavior
{
	public void OnApplied( Actor actor, StatusInstance instance )
		=> actor.ApplyStun( instance.Def.Duration * instance.Potency, ragdoll: false ); // stands frozen, no ragdoll

	public void OnExpired( Actor actor, StatusInstance instance ) { } // the stun timer ends on its own
}

public sealed class KnockDownBehavior : IStatusBehavior
{
	public void OnApplied( Actor actor, StatusInstance instance )
		=> actor.ApplyStun( instance.Def.Duration * instance.Potency );

	public void OnExpired( Actor actor, StatusInstance instance ) { }
}

public sealed class KnockUpBehavior : IStatusBehavior
{
	public float Force = 5000f; // same scale as RuneLibrary KnockbackForce

	public void OnApplied( Actor actor, StatusInstance instance )
	{
		// Ragdoll first so the impulse reaches the bone bodies, then let physics carry it.
		actor.ApplyStun( instance.Def.Duration * instance.Potency );
		CombatMath.ApplyKnockback( actor.GameObject, Vector3.Up, Force * instance.Potency, addUpwardBias: false, preventGrounding: true );
	}

	public void OnExpired( Actor actor, StatusInstance instance ) { }
}

public sealed class ShockBehavior : IStatusBehavior
{
	public float EnergyDrainFraction = 0.10f; // of max energy, per proc
	public float ArcRadius = 300f;
	public float ArcDamageFraction = 0.5f;    // of the causing hit

	public void OnApplied( Actor actor, StatusInstance instance )
	{
		var sheet = actor.StatSheet;
		if ( sheet?.MaxEnergy != null )
			sheet.CurrentEnergy = MathF.Max( 0f, sheet.CurrentEnergy - sheet.MaxEnergy.Value * EnergyDrainFraction * instance.Potency );

		// Enemies lose cast progress; the player only loses energy (no interrupts on the player for now).
		if ( actor is not Enemy ) return;
		actor.NotifyHitReceived();
		Arc( actor, instance );
	}

	private void Arc( Actor origin, StatusInstance instance )
	{
		if ( instance.HitDamage <= 0f ) return;

		Enemy nearest = null;
		float best = ArcRadius * ArcRadius;
		var from = origin.GameObject.WorldPosition;
		foreach ( var other in origin.Scene.GetAllComponents<Enemy>() )
		{
			if ( other == origin || other.StateComp?.CurrentState == ActorStateType.Dead ) continue;
			float d = (other.GameObject.WorldPosition - from).LengthSquared;
			if ( d < best ) { best = d; nearest = other; }
		}

		// IsDot: no stagger, no proc events, no status roll, so arcs can't chain.
		nearest?.ApplyDamage( new DamageProfileDef
		{
			HealthDamage = instance.HitDamage * ArcDamageFraction * instance.Potency,
			Tags = new() { AttackTag.Air },
			IsDot = true
		}, instance.Source );
	}

	public void OnExpired( Actor actor, StatusInstance instance ) { }
}

/// <summary>Enemies swap to FleeBehavior; the player's Fear is just PlayerModifiers (handled by StatusControl).</summary>
public sealed class FearBehavior : IStatusBehavior
{
	public void OnApplied( Actor actor, StatusInstance instance )
	{
		if ( actor is not Enemy enemy ) return;
		var flee = actor.Components.GetOrCreate<FleeBehavior>();
		flee.From = instance.Source;
		enemy.SetBehaviorOverride( flee );
	}

	public void OnExpired( Actor actor, StatusInstance instance )
	{
		if ( actor is not Enemy enemy || actor.Statuses.Has( StatusType.Fear ) ) return;
		enemy.SetBehaviorOverride( null );
		actor.Components.Get<FleeBehavior>()?.Destroy();
	}
}
