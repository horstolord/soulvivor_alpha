using System;
using System.Collections.Generic;
using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

/// Shared combat calculations used by melee and spells so rules stay in sync.
public static class CombatMath
{
	/// <summary>
	/// Resolves the most-derived Actor component on or above <paramref name="gameObject"/>.
	/// Prefers subclasses (e.g. Enemy, Player) over the base Actor type.
	/// Moved here from CombatComponent so every pipeline can share it.
	/// </summary>
	public static Actor ResolveActor( GameObject gameObject )
	{
		if ( gameObject == null ) return null;
		return gameObject.Components.GetAll<Actor>()
			.OrderByDescending( a => a.GetType() != typeof(Actor) )
			.FirstOrDefault()
			?? gameObject.Components.GetInAncestorsOrSelf<Actor>();
	}

	/// <summary>
	/// Returns the root GameObject that owns the Actor component.
	/// Used for hit deduplication across child colliders.
	/// Returns null if no Actor is found in the hierarchy.
	/// </summary>
	public static GameObject ResolveActorRoot( GameObject gameObject )
	{
		if ( gameObject == null ) return null;
		var actor = gameObject.Components.GetAll<Actor>().FirstOrDefault()
		            ?? gameObject.Components.GetInAncestorsOrSelf<Actor>();
		return actor?.GameObject;
	}

	/// <summary>
	/// Applies outgoing modifiers (crit roll, PhysicalForce stat) to a base damage profile.
	/// This is the attacker-side pass; it must run before the hit is applied so that
	/// DamageBoost / Berserk buffs correctly raise outgoing damage, not incoming.
	/// </summary>
	public static DamageProfileDef BuildOutgoing( StatSheet attackerSheet, DamageProfileDef baseDamage )
	{
		if ( baseDamage == null ) return null;
		var damage = RollCrit( attackerSheet, baseDamage );
		if ( attackerSheet != null )
		{
			// DamageMultiplier is an outgoing multiplier (DamageBoost, Berserk, etc.).
			// Applied here on the attacker side so buffs raise dealt damage, not taken damage.
			damage.HealthDamage *= attackerSheet.DamageMultiplier.Value / 100f;
			damage.KnockbackForce *= attackerSheet.PhysicalForce.Value / 100f;
		}
		return damage;
	}

	/// Rolls crit from the attacker's sheet and returns a new damage profile
	/// with Health/Stagger scaled.
	public static DamageProfileDef RollCrit( StatSheet attackerSheet, DamageProfileDef baseDamage )
	{
		if ( baseDamage == null )
			return null;

		if ( attackerSheet == null )
			return baseDamage;

		float critChance = attackerSheet.CritChance.Value;
		bool isCrit = critChance > 0f && Random.Shared.NextSingle() * 100f < critChance;
		float critMultiplier = isCrit ? (1f + attackerSheet.CritDamage.Value / 100f) : 1f;

		return new DamageProfileDef
		{
			HealthDamage = baseDamage.HealthDamage * critMultiplier,
			StaminaDamage = baseDamage.StaminaDamage,
			KnockbackForce = baseDamage.KnockbackForce,
			Tags = baseDamage.Tags,
			IsCrit = isCrit
		};
	}

	/// Scales a damage profile by charge progress. bonusAtMaxCharge is the extra HealthDamage
	/// granted at Charge01=1, already attribute-resolved by the caller (e.g. Might * MightToChargeBonus).
	/// Charge01=0 (a normal tap attack) is a no-op, so callers don't need to branch on whether the
	/// swing was actually charged.
	public static DamageProfileDef ApplyCharge( DamageProfileDef baseDamage, float charge01,
		float bonusAtMaxCharge, float knockbackBonusAtMaxCharge = 0f )
	{
		if ( baseDamage == null )
			return null;

		if ( charge01 <= 0f || (bonusAtMaxCharge == 0f && knockbackBonusAtMaxCharge == 0f) )
			return baseDamage;

		float t = MathF.Max( 0f, MathF.Min( 1f, charge01 ) );

		return new DamageProfileDef
		{
			HealthDamage = baseDamage.HealthDamage + bonusAtMaxCharge * t,
			StaminaDamage = baseDamage.StaminaDamage,
			KnockbackForce = baseDamage.KnockbackForce + knockbackBonusAtMaxCharge * t,
			Tags = baseDamage.Tags,
			IsCrit = baseDamage.IsCrit
		};
	}

	public static void ApplyKnockback( GameObject target, Vector3 direction, float force, bool addUpwardBias = true, bool preventGrounding = false, Vector3? hitPoint = null )
	{
		if ( target == null || force <= 0f )
			return;
 
		var normalizedDirection = direction.LengthSquared > 0.0001f ? direction.Normal : Vector3.Up;
		var knockbackDirection = addUpwardBias
			? (normalizedDirection + Vector3.Up / 2f).Normal
			: normalizedDirection;
		var impulse = knockbackDirection * force;

		var actor = target.Components.GetInAncestorsOrSelf<Actor>();
		var ragdoll = target.Components.GetInAncestorsOrSelf<IRagdollHandler>()
			?? actor?.Components.GetInChildren<IRagdollHandler>();
		if ( ragdoll?.TryApplyImpulse( impulse, hitPoint ) == true )
			return;

		var controller = target.Components.GetInAncestorsOrSelf<CharacterController>();
		if ( controller != null )
		{
			if ( preventGrounding )
				target.Components.GetInAncestorsOrSelf<PlayerController>()?.PreventGrounding( 0.5f );
			controller.Punch( impulse );
			return;
		}

		var rigidbody = target.Components.GetInAncestorsOrSelf<Rigidbody>();
		rigidbody?.ApplyImpulse( impulse );
	}

	/// <summary>Returns one stable object for deduplicating actor and compound-rigidbody hits.</summary>
	public static GameObject GetKnockbackRoot( GameObject target )
	{
		if ( target == null ) return null;

		var actor = target.Components.GetInAncestorsOrSelf<Actor>();
		if ( actor != null ) return actor.GameObject;

		var controller = target.Components.GetInAncestorsOrSelf<CharacterController>();
		if ( controller != null ) return controller.GameObject;

		return target.Components.GetInAncestorsOrSelf<Rigidbody>()?.GameObject;
	}
}

/// <summary>
/// Shared hit-resolution pipeline used by melee, arrows, and spells.
/// Phase 1: all helpers are in place but nothing routes through Apply yet —
/// that happens in Phase 2 (spells) and Phase 3 (melee/arrows).
/// </summary>
public static class HitResolver
{
	/// <summary>
	/// Applies outgoing modifiers, deduplicates by actor-root, applies damage and knockback.
	/// Returns true if the hit was actually applied (i.e. target was not already in alreadyHit).
	/// </summary>
	/// <param name="attacker">The caster/attacker GameObject (used to look up the StatSheet).</param>
	/// <param name="target">The collider or actor that was struck.</param>
	/// <param name="baseDamage">Pre-crit, pre-PhysicalForce damage profile.</param>
	/// <param name="direction">Raw hit direction. ApplyKnockback adds its own upward bias internally.</param>
	/// <param name="alreadyHit">
	///   Optional per-call dedup set. Pass a shared HashSet across multiple calls (e.g. AoE loops)
	///   to prevent the same actor being hit more than once per delivery. Pass null for single-target hits.
	/// </param>
	/// <param name="hitPoint">World-space hit point, forwarded to ragdoll impulse if present.</param>
	public static bool Apply(
		GameObject attacker,
		GameObject target,
		DamageProfileDef baseDamage,
		Vector3 direction,
		HashSet<GameObject> alreadyHit = null,
		Vector3? hitPoint = null )
	{
		if ( target == null || baseDamage == null ) return false;

		// Resolve to actor root for deduplication (fixes multi-collider multi-hit bug).
		var actorRoot = CombatMath.ResolveActorRoot( target );
		var dedupKey  = actorRoot ?? target;

		if ( alreadyHit != null )
		{
			if ( !alreadyHit.Add( dedupKey ) )
				return false; // already processed this actor this delivery
		}

		var attackerSheet = CombatMath.ResolveActor( attacker )?.StatSheet;
		var damage = CombatMath.BuildOutgoing( attackerSheet, baseDamage );

		Log.Info( $"[HitResolver] target={target.Name} health={damage.HealthDamage:F1} knockback={damage.KnockbackForce:F1} dir={direction} crit={damage.IsCrit}" );

		// preventGrounding: true so the target's PlayerController doesn't snap back to ground
		// immediately after the impulse — fixes the "doesn't work if hit only once" issue.
		CombatMath.ApplyKnockback( target, direction, damage.KnockbackForce, preventGrounding: true, hitPoint: hitPoint );

		var actor = CombatMath.ResolveActor( target );
		actor?.ApplyDamage( damage );

		return true;
	}
}

