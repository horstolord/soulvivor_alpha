namespace Sandbox.Code.Systems;

public class DamageProfileDef
{
	public float HealthDamage;
	public float StaminaDamage;
	public float KnockbackForce;
	public HashSet<AttackTag> Tags = new();
	public bool IsCrit;
	/// <summary>True for periodic status damage; prevents that damage from proccing statuses again.</summary>
	public bool IsDot;
	public int ProcDepth;

	// Elemental components on top of HealthDamage. Filled in by CombatMath.BuildOutgoing from the attacker's
	// PhysicalAs*/Added*Damage stats; each one is mitigated by its own resistance in Actor.ApplyDamage.
	public float ExtraFire;
	public float ExtraFrost;
	public float ExtraAir;
	public float ExtraEarth;

	public bool HasExtras => ExtraFire > 0f || ExtraFrost > 0f || ExtraAir > 0f || ExtraEarth > 0f;

	/// <summary>Shallow copy (Tags is shared, treat it as read-only). Use instead of hand-copying fields.</summary>
	public DamageProfileDef Clone() => new()
	{
		HealthDamage = HealthDamage,
		StaminaDamage = StaminaDamage,
		KnockbackForce = KnockbackForce,
		Tags = Tags,
		IsCrit = IsCrit,
		IsDot = IsDot,
		ProcDepth = ProcDepth,
		ExtraFire = ExtraFire,
		ExtraFrost = ExtraFrost,
		ExtraAir = ExtraAir,
		ExtraEarth = ExtraEarth
	};
}

public class HitPhaseDef
{
	public float StartTime;
	public float EndTime;
	public List<HitShapeDef> Shapes = new();
	public bool StopAfterFirstHit;
	public bool IndependentHits;
}


public enum HitShapeCastType
{
	Overlap,
	Sweep,
}
public class HitShapeDef
{
	public HitShapeCastType CastType;
	public Vector3 LocalOffset;
	public Vector3 SweepOffset;
	public Vector3 BoxSize;

	public HitShapeDef ScaledReach( float multiplier ) => new()
	{
		CastType = CastType,
		LocalOffset = LocalOffset.WithX( LocalOffset.x * multiplier ),
		SweepOffset = SweepOffset.WithX( SweepOffset.x * multiplier ),
		BoxSize = BoxSize.WithX( BoxSize.x * multiplier )
	};
}

public enum AttackTag
{
	Melee,
	Ranged,
	Unarmed,
	Kick,
	Strike,
	Slash,
	Pierce,
	Projectile,
	Spell,
	Fire,
	Frost,
	Air,
	Earth,
	Physical,
	Stab,
	Smash,
	Bleed
}
public class AttackDef : ICostable
{
	public string Id;
	public string DisplayName;
	public float StartupTime;
	public float RecoveryTime;
	public float CooldownTime;
	public float HealthCost { get; set; }
	public float StaminaCost { get; set; }
	public float EnergyCost { get; set; }
	public DamageProfileDef Damage = new();
	/// <summary>Fraction of StatSheet.WeaponDamage added to health damage. Defaults to zero.</summary>
	public float WeaponDamageEffectiveness;
	public AttributeScalingDef Scaling = new();
	public List<HitPhaseDef> HitPhases = new();
	public HashSet<AttackTag> Tags = new();
	public string AnimationName;
	public bool LockFacing;
	public bool CanMoveDuringStartup;
	public bool CanMoveDuringRecovery;
	public string ProjectilePrefabPath;
	public ProjectileTemplate ProjectileTemplate; //null=melee
	public string SwingSound; // Soundevent path, null=silent
	public string SwingVfxPrefab; // trail/arc
}
public class AttributeScalingDef
{
	public float MightToHealthDamage;
	public float MightToStaminaDamage;
	public float MightToStaggerDamage;
	public float MightToKnockbackForce;

	public float SwiftnessToHealthDamage;
	public float AcuityToEnergyDamage;

	/// <summary>Extra HealthDamage at Charge01=1 per point of Might.</summary>
	public float MightToChargeBonus;

	public float MightToChargeKnockback;
	/// <summary>Charge ramp speed per point of Swiftness — same curve shape as RuneScalingDef.AcuityToCastSpeed.</summary>
	public float SwiftnessToChargeSpeed;
}
