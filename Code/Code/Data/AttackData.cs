using System;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.Code.Data;
using Sandbox.Code.Systems;


public static class AttackData
{
	private enum MeleeWeaponStyle
	{
		Slash,
		Stab,
		Smash
	}

	public static AttackDef Kick => new AttackDef
	{
		Id = "kick",
		DisplayName = "Kick",
		StartupTime = 0.1f,
		RecoveryTime = 0.25f,
		CooldownTime = 0.01f,
		StaminaCost = 10f,
		Damage = new DamageProfileDef
		{
			HealthDamage = 10f,
			StaminaDamage = 24f,
			KnockbackForce = 200f
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
		LockFacing = false,
		CanMoveDuringStartup = true,
		CanMoveDuringRecovery = true,
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
				StartTime = 0.15f,
				EndTime = 0.3f,
				StopAfterFirstHit = false,
				Shapes = new List<HitShapeDef>
				{
					new HitShapeDef
					{
						CastType = HitShapeCastType.Sweep,
						LocalOffset = new Vector3( 45f, 0f, 55f ),
						SweepOffset = new Vector3( 60f, 0f, 0f ),
						BoxSize = new Vector3( 100f, 60f, 60f )
					}
				}
			}
		}
	};

	public static AttackDef Punch => new AttackDef
	{
		Id = "punch",
		DisplayName = "Punch",
		StartupTime = 0.1f,
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
		StartupTime = 0.3f,
		RecoveryTime = 0.05f,
		CooldownTime = 0.3f,
		StaminaCost = 8f,
		Damage = new DamageProfileDef
		{
			HealthDamage = 8f,
			KnockbackForce = 500f
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
			Termination = ProjectileTerminationType.PierceCount,
			PierceCount = 2,
			StickOnHit = true,
			KillsRefundPierce = true,
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
		var equipment = weapon.Equipment;

		if ( equipment?.RangedWeapon is { } ranged )
		{
			return new AttackDef
			{
				Id = $"weapon_{weapon.Id}",
				DisplayName = weapon.Name,
				StartupTime = ranged.DrawTime,
				RecoveryTime = 0.2f / speed,
				CooldownTime = 0.3f / speed,
				StaminaCost = 8f,
				Damage = new DamageProfileDef
				{
					HealthDamage = stats.BaseDamage,
					StaminaDamage = 12f,
					KnockbackForce = 100f
				},
				WeaponDamageEffectiveness = 1f,
				Scaling = equipment.Scaling ?? new AttributeScalingDef
				{
					MightToHealthDamage = 1f,
					SwiftnessToHealthDamage = 1f
				},
				AnimationName = "attack_shoot",
				LockFacing = true,
				CanMoveDuringStartup = false,
				CanMoveDuringRecovery = false,
				Tags = new HashSet<AttackTag> { AttackTag.Ranged, AttackTag.Projectile, AttackTag.Pierce, AttackTag.Physical },
				HitPhases = new List<HitPhaseDef>(),
				ProjectilePrefabPath = ranged.ProjectilePrefabPath,
				ProjectileTemplate = ranged.ProjectileTemplate?.Clone()
			};
		}

		var style = GetMeleeWeaponStyle( equipment?.WeaponClass ?? WeaponClass.None );
		var styleTag = style switch
		{
			MeleeWeaponStyle.Stab => AttackTag.Stab,
			MeleeWeaponStyle.Smash => AttackTag.Smash,
			_ => AttackTag.Slash
		};
		var hitShape = style switch
		{
			MeleeWeaponStyle.Stab => new HitShapeDef
			{
				CastType = HitShapeCastType.Sweep,
				LocalOffset = new Vector3( 80f, 0f, 48f ),
				SweepOffset = new Vector3( 45f, 0f, 0f ),
				BoxSize = new Vector3( 90f, 32f, 40f )
			},
			MeleeWeaponStyle.Smash => new HitShapeDef
			{
				CastType = HitShapeCastType.Sweep,
				LocalOffset = new Vector3( 42f, 0f, 88f ),
				SweepOffset = new Vector3( 0f, 0f, -110f ),
				BoxSize = new Vector3( 120f, 120f, 80f )
			},
			_ => new HitShapeDef
			{
				CastType = HitShapeCastType.Sweep,
				LocalOffset = new Vector3( 100f, -40f, 60f ),
				SweepOffset = new Vector3( 10f, 230f, 0f ),
				BoxSize = new Vector3( 100f, 100f, 70f )
			}
		};

		// Scale timing: higher attack speed → shorter windows
		float startup  = 0.25f / speed;
		float active   = 0.25f / speed;   // half width of the hit window
		float recovery = 0.2f / speed;
		float cooldown = 0.2f / speed;

		return new AttackDef
		{
			Id          = $"weapon_{weapon.Id}_{style.ToString().ToLowerInvariant()}",
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
			Scaling = equipment?.Scaling ?? new AttributeScalingDef
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
			Tags = style == MeleeWeaponStyle.Stab
				? new HashSet<AttackTag> { AttackTag.Melee, AttackTag.Strike, AttackTag.Stab, AttackTag.Pierce, AttackTag.Physical }
				: new HashSet<AttackTag> { AttackTag.Melee, AttackTag.Strike, styleTag, AttackTag.Physical },
			HitPhases = new List<HitPhaseDef>
			{
				new HitPhaseDef
				{
					StartTime        = 0f,
					EndTime          = active * 2f,
					StopAfterFirstHit = false,
					Shapes = new List<HitShapeDef>
					{
						hitShape
					}
				}
			}
		};
	}

	private static MeleeWeaponStyle GetMeleeWeaponStyle( WeaponClass weaponClass ) => weaponClass switch
	{
		WeaponClass.Spear => MeleeWeaponStyle.Stab,
		WeaponClass.Hammer or WeaponClass.Mace => MeleeWeaponStyle.Smash,
		_ => MeleeWeaponStyle.Slash
	};
}
