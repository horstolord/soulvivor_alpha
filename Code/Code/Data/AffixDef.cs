using System.Collections.Generic;

namespace Sandbox.Code.Data;

/// <summary>
/// One level-gated tier of a rollable stat affix. Multiple AffixDefs share a GroupId to represent
/// tiers of the same underlying stat (e.g. "might_damage" tier 1/2/3) — only one tier per group can
/// roll on a given item.
/// </summary>
public class AffixDef
{
	public string Id;
	public string GroupId;
	public string StatName;      // must match a case in StatSheet.GetStat, e.g. "Might", "Armor"
	public int Tier = 1;
	public int MinLevel = 1;     // source actor's LevelComponent.Level must be >= this to roll
	public float MinValue;
	public float MaxValue;
	public float Weight = 1f;
	public ModifierType Type = ModifierType.Flat;
	public HashSet<string> RequiredTags = new(); // matches ItemDef.Tags; empty = universal (any item)
}
