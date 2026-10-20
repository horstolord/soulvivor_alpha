using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Shared "apply a list of ModData to a StatSheet under a source" helper, used by equipment, boons and
/// (next) elite affixes so the add/remove/attribute-recalc logic lives in one place.
/// Callers keep their own `applied` list (so removal is exact) and decide when to RecalculateDerivedStats
/// (use TouchesAttribute on the mods they applied/removed).
/// </summary>
public static class StatModApplier
{
	public static bool IsAttributeStat( string statName ) =>
		statName is "Might" or "Swiftness" or "Endurance" or "Will" or "Acuity" or "Wisdom";

	public static bool TouchesAttribute( IEnumerable<ModData> mods )
	{
		if ( mods == null ) return false;
		foreach ( var mod in mods )
		{
			if ( IsAttributeStat( mod.StatName ) ) return true;
		}
		return false;
	}

	/// <summary>
	/// Adds every mod at (value x multiplier) as a StatModifier tagged with <paramref name="source"/> and records it
	/// in <paramref name="applied"/>. Unknown stat names are skipped with a warning. Returns how many were applied.
	/// </summary>
	public static int Apply( StatSheet sheet, IEnumerable<ModData> mods, float multiplier, object source,
		ICollection<(Stat Stat, StatModifier Modifier)> applied, string label = null )
	{
		if ( sheet == null || mods == null ) return 0;

		int count = 0;
		foreach ( var mod in mods )
		{
			var stat = sheet.GetStat( mod.StatName );
			if ( stat == null )
			{
				Log.Warning( $"[StatModApplier] '{label ?? "?"}' references unknown stat '{mod.StatName}'" );
				continue;
			}

			var modifier = new StatModifier( mod.Value * multiplier, mod.Type, source );
			stat.AddModifier( modifier );
			applied?.Add( (stat, modifier) );
			count++;
		}
		return count;
	}

	/// <summary>Removes every recorded modifier from its stat and clears the list.</summary>
	public static void Remove( ICollection<(Stat Stat, StatModifier Modifier)> applied )
	{
		if ( applied == null ) return;

		foreach ( var (stat, modifier) in applied )
			stat.RemoveModifier( modifier );
		applied.Clear();
	}
}
