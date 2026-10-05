using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

public class SpellContext : ICostable
{
	// Caster & Aim Context
	public GameObject Caster;
	public Vector3 Origin;
	public Vector3 AimDirection;
	public Vector3? TargetPoint;

	// ICostable Accumulated Totals
	public float TotalHealthCost;
	public float TotalStaminaCost;
	public float TotalEnergyCost;

	public float HealthCost => TotalHealthCost;
	public float StaminaCost => TotalStaminaCost;
	public float EnergyCost => TotalEnergyCost;

	public float TotalCastDelay;
	public float Charge01;

	// Active Modifier Stack (Mutated by Modifier Runes)
	public float DamageMultiplier = 1.0f;
	public float SpeedMultiplier = 1.0f;
	public int BonusPierce = 0;
	public float SpreadAngle = 0f;
	public bool EnableHoming = false;
	public float HomingStrength = 0f;
	public HashSet<RuneElementTag> ElementTags = new();
	/// <summary>First Force rune's element (first wins). Drives prefab/effect variants. Force runes must precede the Method rune.</summary>
	public RuneElementTag? PrimaryElement;
	public HashSet<AttackTag> AttackTags = new();
	public Material VisualMaterial; // Set by whichever Force rune supplies one (first wins)

	// Aggregated Elemental & Physical Damage Profile
	public DamageProfileDef AccumulatedDamage = new();

	// Nested Trigger Runes for OnHit / Expiration Sub-Spells
	public List<RuneDef> TriggerPayloadRunes = new();
	public List<SpellEffect> Effects = new();

	// Multicast / Branching Draw Count
	public int MulticastCount = 1;

	// Recursion Limit Safety
	public int RecursionDepth = 0;
	public const int MaxRecursionDepth = 4;

	/// <summary>
	/// Constructs the base (pre-crit, pre-PhysicalForce) damage profile from this context.
	/// Delivery methods call this once and pass the result to HitResolver.Apply, which
	/// then applies attacker-side outgoing modifiers per hit.
	/// </summary>
	public DamageProfileDef BuildDamageProfile()
	{
		return new DamageProfileDef
		{
			HealthDamage  = AccumulatedDamage.HealthDamage * DamageMultiplier,
			StaminaDamage = AccumulatedDamage.StaminaDamage,
			KnockbackForce = AccumulatedDamage.KnockbackForce,
			Tags = AttackTags
		};
	}

	public void ApplyCharge( float charge01, ChargeScalingDef scaling, float will )
	{
		Charge01 = Math.Clamp( charge01, 0f, 1f );
		if ( Charge01 <= 0f ) return;

		scaling ??= ChargeScalingDef.Default;
		float damageBonus = MathF.Max( 0f, scaling.DamageBonusAtMaxCharge + will * scaling.WillToDamageAtMaxCharge );
		float damageMultiplier = 1f + damageBonus * Charge01;
		float costMultiplier = 1f + MathF.Max( 0f, scaling.CostIncreaseAtMaxCharge ) * Charge01;

		AccumulatedDamage.HealthDamage *= damageMultiplier;
		AccumulatedDamage.StaminaDamage *= damageMultiplier;
		AccumulatedDamage.KnockbackForce *= damageMultiplier;
		foreach ( var effect in Effects )
		{
			if ( effect == null ) continue;
			// Imbue is a ratio of the (already charge-scaled) Force damage; scaling it again would square the bonus.
			if ( effect.Type == SpellEffectType.Imbue ) continue;
			effect.Strength *= damageMultiplier;
			effect.PotencyMultiplier *= damageMultiplier;
		}
		TotalHealthCost *= costMultiplier;
		TotalStaminaCost *= costMultiplier;
		TotalEnergyCost *= costMultiplier;
	}

	public SpellContext Clone()
	{
		return new SpellContext
		{
			Caster = Caster,
			Origin = Origin,
			AimDirection = AimDirection,
			TargetPoint = TargetPoint,
			TotalHealthCost = TotalHealthCost,
			TotalStaminaCost = TotalStaminaCost,
			TotalEnergyCost = TotalEnergyCost,
			TotalCastDelay = TotalCastDelay,
			Charge01 = Charge01,
			DamageMultiplier = DamageMultiplier,
			SpeedMultiplier = SpeedMultiplier,
			BonusPierce = BonusPierce,
			SpreadAngle = SpreadAngle,
			EnableHoming = EnableHoming,
			HomingStrength = HomingStrength,
			ElementTags = new HashSet<RuneElementTag>( ElementTags ),
			PrimaryElement = PrimaryElement,
			AttackTags = new HashSet<AttackTag>( AttackTags ),
			VisualMaterial = VisualMaterial,
			AccumulatedDamage = new DamageProfileDef
			{
				HealthDamage = AccumulatedDamage.HealthDamage,
				StaminaDamage = AccumulatedDamage.StaminaDamage,
				KnockbackForce = AccumulatedDamage.KnockbackForce
			},
			TriggerPayloadRunes = new List<RuneDef>( TriggerPayloadRunes ),
			Effects = Effects.ConvertAll( effect => effect?.Clone() ),
			MulticastCount = MulticastCount,
			RecursionDepth = RecursionDepth
		};
	}
}
