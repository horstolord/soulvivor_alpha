namespace Sandbox.Code.Systems;

using System.Collections.Generic;



/// <summary>How a technique executes: a swing through CombatComponent, a held channel, or an airborne leap + landing slam.</summary>
public enum TechniqueForm
{
	Swing,
	Leap,
	Channel
}

public enum TechniqueUseResult
{
	Started,
	NoTechnique,
	NoCaster,
	OnCooldown,
	Blocked,
	WrongWeapon,
	NoResources,
	/// <summary>Pressed before the recovery window: the regular swing was cancelled and the technique did not fire.</summary>
	EarlyCancel,
	Failed
}

/// <summary>
/// Static definition of a martial technique (the melee counterpart of a spell). Authored timings are at
/// 1.0x attack speed — the live AttackSpeed stat scales them when the swing is built (CombatComponent.BuildContext).
/// Damage is not authored here: it is the equipped weapon's attack × DamageMultiplier.
/// </summary>
/// <summary>Leap form tuning: the launch impulse and how landing speed/equipped weight boost the slam.</summary>
public class LeapSpec
{
	/// <summary>Launch pitch above the aim direction, in degrees.</summary>
	public float LaunchAngleDegrees = 40f;
	/// <summary>Same scale as RuneLibrary.Launch (goes through CombatMath.ApplyKnockback).</summary>
	public float LaunchStrength = 9000f;
	/// <summary>Seconds after launch before "not on ground" is trusted for touchdown detection.</summary>
	public float LaunchGraceTime = 0.4f;
	/// <summary>Safety cap: leap is cancelled if still airborne after this long.</summary>
	public float MaxAirTime = 4f;

	/// <summary>Damage bonus per 100 units/s of landing speed, capped at MaxSpeedBonus.</summary>
	public float SpeedBonusPer100Units = 0.15f;
	public float MaxSpeedBonus = 1.5f;
	/// <summary>Damage bonus per unit of equipped weight, capped at MaxWeightBonus.</summary>
	public float WeightBonusPerUnit = 0.03f;
	public float MaxWeightBonus = 0.6f;
}

public class TechniqueDef : ICostable
{
	public string Id;
	public string DisplayName;
	public string Description;
	public TechniqueForm Form = TechniqueForm.Swing;

	// ICostable — paid by CombatComponent when the swing starts (Swing form).
	public float HealthCost { get; set; }
	public float StaminaCost { get; set; }
	public float EnergyCost { get; set; }

	/// <summary>Technique's own cooldown, independent of the regular swing cooldown.</summary>
	public float Cooldown;

	/// <summary>Regular-swing cooldown applied after this technique ends.</summary>
	public float SwingCooldown = 0.1f;

	/// <summary>
	/// True = can be launched out of a swing's recovery (and ignores the swing cooldown).
	/// Pressing it earlier than the recovery window cancels the swing instead (see TechniqueComponent).
	/// False = only launches from idle; pressing it mid-swing is refused.
	/// </summary>
	public bool SwingReset = true;

	public float DamageMultiplier = 1f;
	public float KnockbackMultiplier = 1f;
	/// <summary>Scales the poise/stamina damage of the weapon attack.</summary>
	public float StaggerMultiplier = 1f;

	public float StartupTime;
	public float RecoveryTime;

	/// <summary>Channel form only: tick rate, per-second costs, move penalty. Per-tick damage is DamageMultiplier.</summary>
	public ChannelSpec Channel;

	/// <summary>
	/// Leap form only: launch impulse and the speed/weight damage bonuses. The landing slam is the technique's
	/// own StartupTime / RecoveryTime / HitPhases, played as a normal swing on touchdown.
	/// </summary>
	public LeapSpec Leap;

	/// <summary>Radius of the radial hit area for the channel form (Whirlwind). Scaled by TechniqueContext.AreaMultiplier.</summary>
	public float Radius = 150f;

	/// <summary>Phase times are relative to the end of startup, same as AttackDef.</summary>
	public List<HitPhaseDef> HitPhases = new();
	public HashSet<AttackTag> Tags = new();

	/// <summary>Empty = any weapon or unarmed. Otherwise the equipped weapon needs at least one of these item tags.</summary>
	public HashSet<string> RequiredWeaponTags = new();

	/// <summary>Null = use the weapon attack's animation name.</summary>
	public string AnimationName;
	public bool LockFacing = true;
	public bool CanMoveDuringStartup;
	public bool CanMoveDuringRecovery;
}
