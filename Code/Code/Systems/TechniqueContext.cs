using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Per-use, mutable resolution of a TechniqueDef — the melee counterpart of SpellContext.
/// Technique modifier runes (later) mutate the multiplier fields / phases before an executor runs it.
/// </summary>
public class TechniqueContext : ICostable
{
	public TechniqueDef Technique;
	public Actor Actor;
	public GameObject Caster;
	public Vector3 Origin;
	public Rotation Facing;
	public Vector3 AimDirection => Facing.Forward;
	public float Charge01;
	public AttackTriggerType TriggerType = AttackTriggerType.PlayerInput;

	/// <summary>The equipped weapon's attack (or Punch when unarmed). Source of damage and scaling.</summary>
	public AttackDef BaseAttack;

	// ===== Modifier stack =====
	public float DamageMultiplier = 1f;
	public float KnockbackMultiplier = 1f;
	public float StaggerMultiplier = 1f;
	/// <summary>
	/// Scales all authored times (startup, phases, recovery, tick interval). Below 1 = faster.
	/// Starts at 1 / the equipped weapon's BaseAttackSpeed, so a technique respects its weapon's own speed;
	/// the live AttackSpeed stat (buffs, affixes) is applied on top of that by CombatComponent / ChannelExecutor.
	/// </summary>
	public float TimeMultiplier = 1f;
	/// <summary>Scales hit-box sizes and sweep lengths.</summary>
	public float AreaMultiplier = 1f;
	public float CostMultiplier = 1f;
	public HashSet<AttackTag> Tags = new();
	public List<HitPhaseDef> HitPhases = new();

	// ICostable — totals after modifiers.
	public float HealthCost => Technique.HealthCost * CostMultiplier;
	public float StaminaCost => Technique.StaminaCost * CostMultiplier;
	public float EnergyCost => Technique.EnergyCost * CostMultiplier;

	/// <summary>Live AttackSpeed stat as a multiplier (1 = 100). Does NOT include the weapon's base speed.</summary>
	public float LiveAttackSpeed => MathF.Max( 0.01f, (Actor?.StatSheet?.AttackSpeed?.Value ?? 100f) / 100f );

	/// <summary>Sum of EquipmentStatBlock.Weight over everything the actor has equipped.</summary>
	public float EquippedWeight => Actor?.GetComponentInChildren<Rigidbody>().PhysicsBody.Mass ?? 0f;

	public static TechniqueContext Create( TechniqueDef def, Actor actor, Vector3 origin, Rotation facing, float charge01 = 0f )
	{
		var weaponStats = actor.Equipment?.GetEquippedWeapon()?.Definition?.Equipment?.Stats;
		float weaponSpeed = weaponStats != null && weaponStats.BaseAttackSpeed > 0f ? weaponStats.BaseAttackSpeed : 1f;

		return new TechniqueContext
		{
			Technique = def,
			Actor = actor,
			Caster = actor.GameObject,
			Origin = origin,
			Facing = facing,
			Charge01 = charge01,
			BaseAttack = actor.Equipment?.GetWeaponAttackDef() ?? AttackData.Punch,
			TimeMultiplier = 1f / weaponSpeed,
			DamageMultiplier = def.DamageMultiplier,
			KnockbackMultiplier = def.KnockbackMultiplier,
			StaggerMultiplier = def.StaggerMultiplier,
			Tags = new HashSet<AttackTag>( def.Tags ),
			HitPhases = ClonePhases( def.HitPhases, 1f, 1f )
		};
	}

	/// <summary>
	/// Compiles this context into a one-off AttackDef for CombatComponent. Only damage and Might scaling are taken
	/// from the weapon attack — timings are the technique's own, so the weapon's speed is not counted twice.
	/// </summary>
	public AttackDef BuildAttackDef()
	{
		var def = Technique;
		var b = BaseAttack;
		var s = b.Scaling ?? new AttributeScalingDef();
		var baseDamage = b.Damage ?? new DamageProfileDef();

		return new AttackDef
		{
			Id = $"technique_{def.Id}",
			DisplayName = def.DisplayName,
			StartupTime = def.StartupTime * TimeMultiplier,
			RecoveryTime = def.RecoveryTime * TimeMultiplier,
			CooldownTime = def.SwingCooldown * TimeMultiplier,
			HealthCost = HealthCost,
			StaminaCost = StaminaCost,
			EnergyCost = EnergyCost,
			Damage = new DamageProfileDef
			{
				HealthDamage = baseDamage.HealthDamage * DamageMultiplier,
				StaminaDamage = baseDamage.StaminaDamage * StaggerMultiplier,
				KnockbackForce = baseDamage.KnockbackForce * KnockbackMultiplier
			},
			// Non-zero for weapon attacks, so a timed weapon imbue (Ember Weapon) also augments techniques.
			WeaponDamageEffectiveness = b.WeaponDamageEffectiveness,
			StatusChanceEffectiveness = b.StatusChanceEffectiveness,
			// Might's contribution scales with the technique too, otherwise the multiplier would only hit the flat part.
			Scaling = new AttributeScalingDef
			{
				MightToHealthDamage = s.MightToHealthDamage * DamageMultiplier,
				MightToStaminaDamage = s.MightToStaminaDamage,
				MightToStaggerDamage = s.MightToStaggerDamage,
				MightToKnockbackForce = s.MightToKnockbackForce,
				SwiftnessToHealthDamage = s.SwiftnessToHealthDamage,
				AcuityToEnergyDamage = s.AcuityToEnergyDamage,
				MightToChargeBonus = s.MightToChargeBonus * DamageMultiplier,
				MightToChargeKnockback = s.MightToChargeKnockback,
				SwiftnessToChargeSpeed = s.SwiftnessToChargeSpeed
			},
			HitPhases = ClonePhases( HitPhases, TimeMultiplier, AreaMultiplier ),
			Tags = new HashSet<AttackTag>( Tags ),
			AnimationName = def.AnimationName ?? b.AnimationName,
			LockFacing = def.LockFacing,
			CanMoveDuringStartup = def.CanMoveDuringStartup,
			CanMoveDuringRecovery = def.CanMoveDuringRecovery
		};
	}

	/// <summary>
	/// Final per-hit damage profile (pre-crit, pre-PhysicalForce — HitResolver applies those) for forms that
	/// resolve hits themselves instead of going through CombatComponent (channels). Mirrors the math in
	/// CombatComponent.BuildContext; TODO: share one helper between the two.
	/// </summary>
	public DamageProfileDef BuildHitDamage()
	{
		var attack = BuildAttackDef();
		var sheet = Actor?.StatSheet;
		float might = sheet?.Might?.Value ?? 0f;

		var damage = new DamageProfileDef
		{
			HealthDamage = attack.Damage.HealthDamage
				+ might * attack.Scaling.MightToHealthDamage
				+ (sheet?.Swiftness?.Value ?? 0f) * attack.Scaling.SwiftnessToHealthDamage
				+ (sheet?.WeaponDamage?.Value ?? 0f) * attack.WeaponDamageEffectiveness,
			StaminaDamage = attack.Damage.StaminaDamage,
			KnockbackForce = attack.Damage.KnockbackForce + might * attack.Scaling.MightToKnockbackForce,
			StatusChanceMultiplier = attack.StatusChanceEffectiveness,
			Tags = new HashSet<AttackTag>( attack.Tags )
		};

		// Timed weapon enchant only augments weapon attacks, same rule as CombatComponent.BuildContext.
		if ( attack.WeaponDamageEffectiveness > 0f )
			damage = Actor?.Components.Get<WeaponImbueControl>()?.Augment( damage ) ?? damage;

		return damage;
	}

	private static List<HitPhaseDef> ClonePhases( List<HitPhaseDef> source, float timeScale, float areaScale )
	{
		var result = new List<HitPhaseDef>();
		if ( source == null ) return result;

		foreach ( var p in source )
		{
			var phase = new HitPhaseDef
			{
				StartTime = p.StartTime * timeScale,
				EndTime = p.EndTime * timeScale,
				StopAfterFirstHit = p.StopAfterFirstHit,
				IndependentHits = p.IndependentHits
			};
			foreach ( var shape in p.Shapes )
			{
				phase.Shapes.Add( new HitShapeDef
				{
					CastType = shape.CastType,
					LocalOffset = shape.LocalOffset,
					SweepOffset = shape.SweepOffset * areaScale,
					BoxSize = shape.BoxSize * areaScale
				} );
			}
			result.Add( phase );
		}
		return result;
	}
}
