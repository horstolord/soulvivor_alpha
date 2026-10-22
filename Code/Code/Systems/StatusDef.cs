using System.Collections.Generic;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Pass 1 ships Bleed/Burn/Chill/Shatter. The control statuses are declared so the enum stays stable,
/// but have no StatusDef in StatusLibrary yet.
/// </summary>
public enum StatusType { Bleed, Burn, Chill, Shatter, Fear, Frozen, Hypothermia, KnockUp, Shock, KnockDown }

/// <summary>Static, data-only definition of a status. Runtime state lives in StatusInstance.</summary>
public class StatusDef
{
	public StatusType Type;
	public string DisplayName;

	/// <summary>Hit tag that makes this status eligible. Every eligible status rolls on its own.</summary>
	public AttackTag RequiredTag;

	/// <summary>Multiplier on the attacker's StatusChance for this status only (e.g. 0.5 = half as likely).</summary>
	public float ChanceScale = 1f;

	public float Duration = 5f;

	/// <summary>Max simultaneous instances per target. At the cap a new proc replaces the oldest one.</summary>
	public int MaxStacks = 1;

	// ---- Damage over time ----
	/// <summary>Seconds between ticks. 0 = no damage over time.</summary>
	public float TickInterval;

	/// <summary>Total damage over the base Duration, as a fraction of the causing hit's health damage.</summary>
	public float DamageFraction;

	/// <summary>Tags on the tick damage. Omit Physical to ignore armor; Fire etc. let resistances apply.</summary>
	public HashSet<AttackTag> DamageTags = new();

	// ---- Stat debuffs (one set per stack, scaled by potency) ----
	public List<BuffModifier> Modifiers = new();

	/// <summary>Fraction of the target's MaxPoise drained on each application.</summary>
	public float PoiseDrainFraction;
}

/// <summary>One proc of a status on one target. Several instances of a type can coexist (stacks).</summary>
public class StatusInstance
{
	public StatusDef Def;
	public GameObject Source;
	public float ExpiresAt;
	public float DamagePerSecond;
	public List<(Stat Stat, StatModifier Modifier)> AppliedModifiers = new();
}
