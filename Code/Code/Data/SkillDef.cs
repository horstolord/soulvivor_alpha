using System;
using System.Collections.Generic;
using Sandbox.Code.Systems;

namespace Sandbox.Code.Data;


public static class SkillIds
{
	public const string Melee = "melee", Ranged = "ranged", Magic = "magic",
		Defense = "defense", Mobility = "mobility",
		Fire = "elem_fire", Frost = "elem_frost", Air = "elem_air", Earth = "elem_earth";
}

/// <summary>Stat bonus granted per skill level (value = PerLevel * level).</summary>
public class SkillStatEffect
{
	public string StatName;
	public float PerLevel;
	public ModifierType Type = ModifierType.Flat;
}

public class SkillDef
{
	public const int MaxLevel = 100;
	public List<ProcDef> XpSources = new();

	public string Id;
	public string DisplayName;
	public string Group;                       // UI grouping, e.g. "Elements"
	public float XpBase = 10f;
	public float XpExponent = 1.6f;            // XP to next = XpBase * level^exp
	public List<SkillStatEffect> StatEffects = new();
	/// <summary>Outgoing hits carrying any of these tags get +DamagePercentPerLevel% per level.</summary>
	public HashSet<AttackTag> DamageTags = new();
	public float DamagePercentPerLevel;

	public float XpToNext( int level ) => XpBase * MathF.Pow( level, XpExponent );
}
