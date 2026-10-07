namespace Sandbox.Code.Systems;

public class DamageProfileDef
{
	public float HealthDamage;
	public float StaminaDamage;
	public float KnockbackForce;
	public HashSet<AttackTag> Tags = new();
	public bool IsCrit;
}

public class HitPhaseDef
{
	public float StartTime;
	public float EndTime;
	public List<HitShapeDef> Shapes = new();
	public bool StopAfterFirstHit;
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
	Smash
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
