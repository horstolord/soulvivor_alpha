using System.Collections.Generic;
using Sandbox.Code.Systems;

namespace Sandbox.Code.Data;

/// <summary>
/// Authored techniques. All numbers are placeholders to tune. Timings are at 1.0x attack speed;
/// hit-phase times are relative to the end of startup (same convention as AttackData).
/// Damage = equipped weapon attack × DamageMultiplier — none is authored here.
/// </summary>
public static class TechniqueLibrary
{
	/// <summary>Two quick diagonal cuts forming an X. Swing reset: cancel into it from a swing's recovery.</summary>
	public static TechniqueDef CrossSlash => new TechniqueDef
	{
		Id = "cross_slash",
		DisplayName = "Cross Slash",
		Description = "Two rapid diagonal cuts. Can cancel the recovery of a swing.",
		Form = TechniqueForm.Swing,
		StaminaCost = 14f,
		Cooldown = 1.5f,
		SwingCooldown = 0.1f,
		SwingReset = true,
		DamageMultiplier = 0.8f,       // per cut — two cuts land
		KnockbackMultiplier = 0.6f,
		StaggerMultiplier = 1f,
		StartupTime = 0.12f,
		RecoveryTime = 0.3f,
		Tags = new HashSet<AttackTag> { AttackTag.Melee, AttackTag.Slash, AttackTag.Physical },
		HitPhases = new List<HitPhaseDef>
		{
			// Independent phases can hit the same target once each; leave a visible beat between the cuts.
			Slash( 0f, 0.1f, new Vector3( 0f, 140f, 60f ) ),
			Slash( 0.35f, 0.45f, new Vector3( 0f, -140f, 60f ) )
		}
	};

	/// <summary>Long wind-up, single huge hit. Committed: cannot be launched out of a swing.</summary>
	public static TechniqueDef MortalBlow => new TechniqueDef
	{
		Id = "mortal_blow",
		DisplayName = "Mortal Blow",
		Description = "A slow, committed strike that hits enormously hard.",
		Form = TechniqueForm.Swing,
		StaminaCost = 28f,
		Cooldown = 6f,
		SwingCooldown = 0.3f,
		SwingReset = false,
		DamageMultiplier = 3.5f,
		KnockbackMultiplier = 2.5f,
		StaggerMultiplier = 3f,
		StartupTime = 0.65f,
		RecoveryTime = 0.55f,
		Tags = new HashSet<AttackTag> { AttackTag.Melee, AttackTag.Strike, AttackTag.Physical },
		HitPhases = new List<HitPhaseDef>
		{
			new HitPhaseDef
			{
				StartTime = 0f,
				EndTime = 0.12f,
				Shapes = new List<HitShapeDef>
				{
					new HitShapeDef
					{
						CastType = HitShapeCastType.Sweep,
						LocalOffset = new Vector3( 50f, 0f, 40f ),
						SweepOffset = new Vector3( 120f, 0f, 0f ),
						BoxSize = new Vector3( 90f, 90f, 90f )
					}
				}
			}
		}
	};

	/// <summary>Hold to spin: a radial hit around you every tick, stamina drains per second, you move slower.</summary>
	public static TechniqueDef Whirlwind => new TechniqueDef
	{
		Id = "whirlwind",
		DisplayName = "Whirlwind",
		Description = "Hold to spin, striking everything around you.",
		Form = TechniqueForm.Channel,
		StaminaCost = 8f,              // flat cost to start; the drain below is on top
		Cooldown = 1f,                 // starts when the channel ends
		SwingReset = false,            // channeled: launches from idle, does not cancel swings
		DamageMultiplier = 0.45f,      // PER TICK
		KnockbackMultiplier = 0.25f,   // per tick — repeated ticks stack, keep it low
		StaggerMultiplier = 0.5f,
		Radius = 160f,
		Channel = new ChannelSpec
		{
			TickInterval = 0.3f,
			StaminaPerSecond = 14f,
			MoveSpeedPenaltyPercent = -35f,
			MaxDuration = 6f,
			EndLockout = 0.3f
		},
		Tags = new HashSet<AttackTag> { AttackTag.Melee, AttackTag.Slash, AttackTag.Physical }
	};

	/// <summary>Launches you up and forwards; on touchdown a chunky slam hits everything around you, boosted by impact speed and equipped weight.</summary>
	public static TechniqueDef LeapAttack => new TechniqueDef
	{
		Id = "leap_attack",
		DisplayName = "Leap Attack",
		Description = "Leap forward and crash down, hitting harder the faster and heavier you land.",
		Form = TechniqueForm.Leap,
		StaminaCost = 18f,             // paid at launch
		Cooldown = 1f,                 // starts at launch
		SwingCooldown = 0.1f,
		SwingReset = false,            // committed: launches from idle only
		DamageMultiplier = 2f,         // before the speed/weight bonus
		KnockbackMultiplier = 1.5f,
		StaggerMultiplier = 2f,
		// The slam is a normal swing played on touchdown: no windup, short active window, landing recovery.
		StartupTime = 0f,
		RecoveryTime = 0.4f,
		HitPhases = new List<HitPhaseDef>
		{
			new HitPhaseDef
			{
				StartTime = 0f,
				EndTime = 0.1f,
				Shapes = new List<HitShapeDef>
				{
					// Big flat box around the landing point (overlap, axis-aligned).
					new HitShapeDef
					{
						CastType = HitShapeCastType.Overlap,
						LocalOffset = new Vector3( 0f, 0f, 60f ),
						BoxSize = new Vector3( 440f, 440f, 160f )
					}
				}
			}
		},
		Leap = new LeapSpec
		{
			LaunchAngleDegrees = 40f,
			LaunchStrength = 9000f,    // same scale as RuneLibrary.Launch — tune in-game
			SpeedBonusPer100Units = 0.15f,
			MaxSpeedBonus = 1.5f,
			WeightBonusPerUnit = 0.03f,
			MaxWeightBonus = 0.6f
		},
		Tags = new HashSet<AttackTag> { AttackTag.Melee, AttackTag.Strike, AttackTag.Physical }
	};

	public static TechniqueDef Resolve( string id ) => id switch
	{
		"cross_slash" => CrossSlash,
		"mortal_blow" => MortalBlow,
		"whirlwind" => Whirlwind,
		"leap_attack" => LeapAttack,
		_ => null
	};

	private static HitPhaseDef Slash( float start, float end, Vector3 sweep ) => new HitPhaseDef
	{
		StartTime = start,
		EndTime = end,
		IndependentHits = true,
		Shapes = new List<HitShapeDef>
		{
			new HitShapeDef
			{
				CastType = HitShapeCastType.Sweep,
				LocalOffset = new Vector3( 60f, 0f, 40f ),
				SweepOffset = sweep,
				BoxSize = new Vector3( 60f, 60f, 60f )
			}
		}
	};
}
