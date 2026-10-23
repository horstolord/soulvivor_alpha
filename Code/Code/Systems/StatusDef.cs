using System.Collections.Generic;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Frozen is never rolled from a hit: it is the escalation of Chill (poise break while chilled).
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

	/// <summary>Stat debuffs used instead of Modifiers when the target is not an Enemy (e.g. Fear: damage debuff). Null = same as Modifiers.</summary>
	public List<BuffModifier> PlayerModifiers;

	// ---- Control ----
	/// <summary>Takes control from the target (stun, launch, flee). Enemies only get an immunity window afterwards.</summary>
	public bool IsCrowdControl;

	/// <summary>Skipped entirely on non-enemies. Fear is not EnemyOnly: the player gets its PlayerModifiers.</summary>
	public bool EnemyOnly;

	/// <summary>Optional special effect on apply/expire. Stateless, shared between all instances.</summary>
	public IStatusBehavior Behavior;
}

/// <summary>Same pattern as IProjectileMotion/IEnemyBehavior: data picks the implementation, no subclassing.</summary>
public interface IStatusBehavior
{
	void OnApplied( Actor actor, StatusInstance instance );
	void OnExpired( Actor actor, StatusInstance instance );
}

/// <summary>One proc of a status on one target. Several instances of a type can coexist (stacks).</summary>
public class StatusInstance
{
	public StatusDef Def;
	public GameObject Source;
	public float ExpiresAt;
	public float DamagePerSecond;
	/// <summary>Post-crit health damage of the causing hit; behaviors (Shock arc) scale from it.</summary>
	public float HitDamage;
	public float Potency = 1f;
	public bool AffectsPools;
	public List<(Stat Stat, StatModifier Modifier)> AppliedModifiers = new();
}
