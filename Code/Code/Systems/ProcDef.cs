using System;
using System.Collections.Generic;
using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

public enum ProcTrigger
{
	HitDealt,   // owner damaged something
	Crit,       // owner landed a crit (raised in addition to HitDealt)
	Kill,       // owner killed something
	HitTaken,   // owner took a non-evaded hit
	Evaded,     // owner evaded a hit
	Blocked,    // owner took a hit while blocking (raised in addition to HitTaken)
	Death,      // owner died
	Dodge,      // owner started a dodge
	Cast,       // owner completed a spell cast
	LevelUp,    // Amount = new level
	Interval    // every ProcDef.IntervalSeconds
}

/// <summary>
/// What happened, from the owner's point of view.
/// Other = the victim for HitDealt/Crit/Kill; the attacker for HitTaken/Evaded/Blocked/Death. May be null.
/// ProcDepth = how many procs deep the cause is (0 = a natural hit/cast, not caused by a proc).
/// </summary>
public struct ProcEvent
{
	public ProcTrigger Trigger;
	public Actor Other;
	public Vector3 Position;
	public float Amount;
	public bool IsCrit;
	public HashSet<AttackTag> Tags; // shared reference — read only
	public int ProcDepth;
}

/// <summary>Flat condition block. Every field left at its default is ignored.</summary>
public struct ProcCondition
{
	/// <summary>Event must carry at least one of these tags (any-of, like AffixDef.RequiredTags).</summary>
	public HashSet<AttackTag> RequiredTags;
	public bool RequireCrit;
	/// <summary>Owner health must be below this percent of max (0 = ignore).</summary>
	public float OwnerHealthBelowPct;
	/// <summary>Event.Amount must be at least this (0 = ignore).</summary>
	public float MinAmount;

	public bool Matches( Actor owner, in ProcEvent ev )
	{
		if ( RequireCrit && !ev.IsCrit ) return false;
		if ( MinAmount > 0f && ev.Amount < MinAmount ) return false;

		if ( RequiredTags != null && RequiredTags.Count > 0
			&& (ev.Tags == null || !RequiredTags.Overlaps( ev.Tags )) )
			return false;

		if ( OwnerHealthBelowPct > 0f )
		{
			var sheet = owner?.StatSheet;
			if ( sheet?.MaxHealth == null ) return false;
			float pct = sheet.CurrentHealth / MathF.Max( 1f, sheet.MaxHealth.Value ) * 100f;
			if ( pct >= OwnerHealthBelowPct ) return false;
		}

		return true;
	}
}

/// <summary>Everything an effect needs when its proc fires.</summary>
public struct ProcContext
{
	public ProcDef Def;
	public Actor Owner;
	public ProcEvent Event;
	/// <summary>Stack/strength multiplier supplied by whatever granted the proc (boon stacks etc.). 1 = normal.</summary>
	public float Potency;
}

public interface IProcEffect
{
	void Execute( in ProcContext ctx );
}

public class ProcDef
{
	public string Id;
	public string DisplayName;
	public ProcTrigger Trigger;

	/// <summary>Percent, same convention as CritChance/Evasion. 100 = always.</summary>
	public float Chance = 100f;

	/// <summary>Seconds after a successful fire before this proc can fire again.</summary>
	public float InternalCooldown;

	/// <summary>Only used by ProcTrigger.Interval.</summary>
	public float IntervalSeconds = 1f;

	public ProcCondition Condition;

	/// <summary>
	/// Highest ProcDepth of an event this proc may react to. 0 = natural events only (no chain procs),
	/// 1 = may also react to events caused by a depth-0 proc, and so on.
	/// </summary>
	public int MaxProcDepth = 0;

	/// <summary>
	/// Chance falloff when reacting to a chained event: chance / (1 + depth) ^ exponent.
	/// 0 = no falloff, 0.5 = square-root falloff, 1 = hyperbolic.
	/// </summary>
	public float ChainFalloffExponent = 0f;

	public List<IProcEffect> Effects = new();
}
