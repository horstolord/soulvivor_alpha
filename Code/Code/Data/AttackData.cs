using System;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.Code.Data;
using Sandbox.Code.Systems;


public static class AttackData
{
	public static AttackDef Kick => new AttackDef
	{
		Id = "kick",
		DisplayName = "Kick",
		StartupTime = 0.18f,
		RecoveryTime = 0.35f,
		CooldownTime = 0.10f,
		StaminaCost = 10f,
		Damage = new DamageProfileDef
		{
			HealthDamage = 10f,
			StaminaDamage = 24f,
			KnockbackForce = 100f
		},
		Scaling = new AttributeScalingDef
		{
			MightToHealthDamage = 2f,
			MightToStaminaDamage = 2f,
			MightToStaggerDamage = 3f,
			MightToKnockbackForce = 100f,
			MightToChargeBonus = 6f,      // TODO tune
			SwiftnessToChargeSpeed = 1.5f // TODO tune
		},
		AnimationName = "attack_kick",
		LockFacing = true,
		CanMoveDuringStartup = false,
		CanMoveDuringRecovery = false,
		Tags = new HashSet<AttackTag>
		{
			AttackTag.Melee,
			AttackTag.Unarmed,
			AttackTag.Kick,
			AttackTag.Strike,
			AttackTag.Physical
		},
		HitPhases = new List<HitPhaseDef>
		{
			new HitPhaseDef
			{
				StartTime = 0.3f,
				EndTime = 0.6f,
				StopAfterFirstHit = true,
				Shapes = new List<HitShapeDef>
				{
					new HitShapeDef
					{
						CastType = HitShapeCastType.Sweep,
						LocalOffset = new Vector3( 45f, 0f, 40f ),
						SweepOffset = new Vector3( 60f, 0f, 0f ),
						BoxSize = new Vector3( 60f, 60f, 60f )
					}
				}
			}
		}
	};

	public static AttackDef Punch => new AttackDef
	{
		Id = "punch",
		DisplayName = "Punch",
		StartupTime = 0f,
		RecoveryTime = 0.2f,
		CooldownTime = 0.2f,
		StaminaCost = 5f,
		Damage =
			new DamageProfileDef
			{
				HealthDamage = 5f, StaminaDamage = 5f,  KnockbackForce = 100f
			},
		Scaling =
			new AttributeScalingDef
			{
				MightToHealthDamage = 2f,
				MightToStaminaDamage = 1f,
				MightToStaggerDamage = 3f,
				MightToKnockbackForce = 50f,
				MightToChargeBonus = 4f,      // TODO tune
				MightToChargeKnockback = 100f,
				SwiftnessToChargeSpeed = 1.5f // TODO tune
			},
		AnimationName = "b_attack",
		LockFacing = true,
		CanMoveDuringStartup = false,
		CanMoveDuringRecovery = false,
		Tags = new HashSet<AttackTag> { AttackTag.Melee, AttackTag.Unarmed, AttackTag.Strike, AttackTag.Physical },
		HitPhases = new List<HitPhaseDef>
		{
			new HitPhaseDef
			{
				StartTime = 0.18f,
				EndTime = 0.28f,
				StopAfterFirstHit = true,
				Shapes = new List<HitShapeDef>
				{
					new HitShapeDef
					{
						CastType = HitShapeCastType.Sweep,
						LocalOffset = new Vector3( 40f, 0f, 40f ),
						SweepOffset = new Vector3( 90f, 0f, 0f ),
						BoxSize = new Vector3( 34f, 42f, 42f )
					}
				}
			}
		}
	};
	public static AttackDef Shoot => new AttackDef
	{
		Id = "shoot",
		DisplayName = "Shoot Arrow",
		StartupTime = 0.15f,
		RecoveryTime = 0.05f,
		CooldownTime = 0.2f,
		StaminaCost = 8f,
		Damage = new DamageProfileDef
		{
			HealthDamage = 8f,
			KnockbackForce = 100f
		},
		Scaling = new AttributeScalingDef
		{
			MightToHealthDamage = 1f,
			MightToKnockbackForce = 50f,
			MightToChargeBonus = 5f,      // TODO tune
			SwiftnessToChargeSpeed = 1.5f // TODO tune
		},
		AnimationName = "attack_shoot",
		LockFacing = true,
		CanMoveDuringStartup = false,
		CanMoveDuringRecovery = false,
		Tags = new HashSet<AttackTag> { AttackTag.Ranged, AttackTag.Projectile, AttackTag.Physical },
		HitPhases = new List<HitPhaseDef>(),
		ProjectileTemplate = new ProjectileTemplate
		{
			Termination = ProjectileTerminationType.FirstHit,
			CollisionBoxSize = new Vector3( 6f, 6f, 6f ),
			Speed = 2000f
		}
	};

	/// <summary>
	/// Builds a weapon-specific AttackDef from an equipped item's stats.
	/// Timing windows are scaled by BaseAttackSpeed (1.0 = normal, 1.5 = 50% faster).
	/// Called each time the player attacks so the def always reflects the current weapon state.
	/// </summary>
	public static AttackDef BuildWeaponAttack( ItemDef weapon )
	{
		var stats = weapon.Equipment?.Stats ?? new EquipmentStatBlock();
		float speed = MathF.Max( 0.1f, stats.BaseAttackSpeed > 0f ? stats.BaseAttackSpeed : 1.0f );

		// Scale timing: higher attack speed → shorter windows
		float startup  = 0.25f / speed;
		float active   = 0.18f / speed;   // half width of the hit window
		float recovery = 0.2f / speed;
		float cooldown = 0.3f / speed;

		return new AttackDef
		{
			Id          = $"weapon_{weapon.Id}",
			DisplayName = weapon.Name,
			StartupTime  = startup,
			RecoveryTime = recovery,
			CooldownTime = cooldown,
			StaminaCost  = 8f,
			Damage = new DamageProfileDef
			{
				HealthDamage  = stats.BaseDamage,
				StaminaDamage = 12f,
				KnockbackForce = 100f
			},
			WeaponDamageEffectiveness = 1f,
			Scaling = new AttributeScalingDef
			{
				MightToHealthDamage    = 2f,
				MightToStaggerDamage   = 1f,
				MightToStaminaDamage   = 0.5f,
				MightToKnockbackForce  = 50f,
				MightToChargeBonus     = 6f,   // TODO tune
				MightToChargeKnockback = 100f,
				SwiftnessToChargeSpeed = 1.5f  // TODO tune
			},
			AnimationName  = "b_attack",
			LockFacing     = true,
			CanMoveDuringStartup  = false,
			CanMoveDuringRecovery = false,
			Tags = new HashSet<AttackTag> { AttackTag.Melee, AttackTag.Strike, AttackTag.Slash, AttackTag.Physical },
			HitPhases = new List<HitPhaseDef>
			{
				new HitPhaseDef
				{
					StartTime        = 0f,
					EndTime          = active * 2f,
					StopAfterFirstHit = false,
					Shapes = new List<HitShapeDef>
					{
						new HitShapeDef
						{
							CastType    = HitShapeCastType.Sweep,
							LocalOffset = new Vector3( 40f, 0f, 40f ),
							SweepOffset = new Vector3( 100f, 0f, 0f ),
							BoxSize     = new Vector3( 70f, 70f, 50f )
						}
					}
				}
			}
		};
	}
}
