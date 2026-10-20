using System;
using Sandbox.Code.Systems;
using Sandbox.Code.World;

namespace Sandbox.Code.Data;

/// <summary>
/// An elite modifier: a bundle of stat mods + procs + body scale + outline colour. Definitions are shared and
/// stateless (like ProcDef): per-mob state lives on EliteControl / the actor, never in here.
/// </summary>
public class EliteAffixDef
{
	public string Id;

	/// <summary>Only one affix per group can roll on a mob (the four infusions share one).</summary>
	public string GroupId;

	public string DisplayName;

	/// <summary>Relative chance among affixes when rolling.</summary>
	public float Weight = 1f;

	/// <summary>Body size multiplier; multiplied across affixes. Reach (Range) follows automatically.</summary>
	public float Scale = 1f;

	public List<ModData> Modifiers = new();
	public List<ProcDef> Procs = new();

	/// <summary>Outline colour and tint. The first affix of an elite wins.</summary>
	public Color OutlineColor = Color.White;

	/// <summary>Reserved for a later aura prefab (particles); unused for now.</summary>
	public string AuraPrefabPath;

	/// <summary>Set by elemental affixes (later: lets other affixes follow the mob's element).</summary>
	public RuneElementTag? Element;
}

/// <summary>
/// All elite affixes plus the roll. PLACEHOLDER numbers throughout — names, values and weights are yours to tune.
/// Built with named methods, not lambdas (static lambdas break on hotreload).
/// </summary>
public static class EliteAffixLibrary
{
	// ===== TUNING — placeholders =====
	/// <summary>Percent chance of a second affix; the chance is multiplied by ExtraAffixFalloff for each further one.</summary>
	public const float ExtraAffixChance = 40f;
	public const float ExtraAffixFalloff = 0.5f;
	public const int MaxAffixes = 4;

	public static List<EliteAffixDef> All { get; } = BuildAll();

	public static EliteAffixDef Get( string id ) => All.FirstOrDefault( a => a.Id == id );

	/// <summary>Product of the affixes' Scale values (1 for none).</summary>
	public static float TotalScale( IEnumerable<EliteAffixDef> affixes )
	{
		float scale = 1f;
		if ( affixes == null ) return scale;
		foreach ( var affix in affixes )
			scale *= affix.Scale;
		return scale;
	}

	/// <summary>
	/// The first affix is guaranteed; each further one is rolled at a falling chance (40%, then 20%, 10%...),
	/// no repeats, and at most one per group.
	/// </summary>
	public static List<EliteAffixDef> RollAffixes()
	{
		var result = new List<EliteAffixDef>();
		var pool = new List<EliteAffixDef>( All );
		float chance = ExtraAffixChance;

		for ( int i = 0; i < MaxAffixes && pool.Count > 0; i++ )
		{
			if ( i > 0 )
			{
				if ( Random.Shared.NextSingle() * 100f >= chance ) break;
				chance *= ExtraAffixFalloff;
			}

			var pick = WeightedPick( pool );
			result.Add( pick );
			pool.RemoveAll( a => a.GroupId == pick.GroupId );
		}

		return result;
	}

	private static EliteAffixDef WeightedPick( List<EliteAffixDef> pool )
	{
		float totalWeight = pool.Sum( a => MathF.Max( 0f, a.Weight ) );
		if ( totalWeight <= 0f ) return pool[Random.Shared.Next( pool.Count )];

		float roll = Random.Shared.NextSingle() * totalWeight;
		float acc = 0f;
		foreach ( var affix in pool )
		{
			acc += MathF.Max( 0f, affix.Weight );
			if ( roll <= acc ) return affix;
		}
		return pool[^1];
	}

	// ============ DEFINITIONS ============

	private static List<EliteAffixDef> BuildAll()
	{
		return new List<EliteAffixDef>
		{
			Volatile(),
			Massive(),
			// Weight 0.25 each so the whole infusion family is as likely as one other affix.
			Infusion( "fire", "Fire-Infused", RuneElementTag.Fire, "ResistanceFire", "PhysicalAsFire", new Color( 1f, 0.55f, 0.15f ) ),
			Infusion( "frost", "Frost-Infused", RuneElementTag.Frost, "ResistanceFrost", "PhysicalAsFrost", new Color( 0.55f, 0.85f, 1f ) ),
			Infusion( "air", "Storm-Infused", RuneElementTag.Air, "ResistanceAir", "PhysicalAsAir", new Color( 0.8f, 1f, 0.9f ) ),
			Infusion( "earth", "Stone-Infused", RuneElementTag.Earth, "ResistanceEarth", "PhysicalAsEarth", new Color( 0.75f, 0.6f, 0.35f ) )
		};
	}

	/// <summary>
	/// Death: three hot flashes, then a shrunken fire nova (750 x 0.35 = ~260 units) at 1.2s — inside the 2s the
	/// corpse stays alive (Actor.OnKilled). The nova hurts every actor nearby, mobs included, and a victim's own
	/// Volatile may chain it: MaxProcDepth 2 with a square-root falloff (100% -> ~71% -> ~58%).
	/// </summary>
	private static EliteAffixDef Volatile()
	{
		var proc = new ProcDef
		{
			Id = "elite_volatile",
			DisplayName = "Volatile",
			Trigger = ProcTrigger.Death,
			Chance = 100f,
			MaxProcDepth = 2,
			ChainFalloffExponent = 0.5f,
			Effects = new List<IProcEffect>
			{
				new FlashProcEffect { Count = 3, Interval = 0.35f, OnTime = 0.15f },
				new DelayedProcEffect
				{
					Delay = 1.2f,
					Inner = new CastRunesProcEffect
					{
						Runes = RuneLibrary.GetPreset( "fire_nova" ),
						RangeScale = 0.35f,
						AimAtOther = false
					}
				}
			}
		};

		return new EliteAffixDef
		{
			Id = "volatile",
			GroupId = "volatile",
			DisplayName = "Volatile",
			OutlineColor = new Color( 1f, 0.35f, 0.1f ),
			Procs = { proc }
		};
	}

	/// <summary>Bigger body (reach follows via EliteControl), double HP and poise on top of the elite baseline.</summary>
	private static EliteAffixDef Massive()
	{
		return new EliteAffixDef
		{
			Id = "massive",
			GroupId = "massive",
			DisplayName = "Massive",
			Scale = 1.6f,
			OutlineColor = new Color( 0.9f, 0.75f, 0.3f ),
			Modifiers =
			{
				new ModData( "MaxHealth", 100f, ModifierType.Percent ),
				new ModData( "MaxPoise", 100f, ModifierType.Percent )
			}
		};
	}

	/// <summary>Resists its element and gains +50% of its physical damage as extra damage of that element.</summary>
	private static EliteAffixDef Infusion( string key, string displayName, RuneElementTag element,
		string resistStat, string physicalAsStat, Color color )
	{
		return new EliteAffixDef
		{
			Id = $"infused_{key}",
			GroupId = "infusion",
			DisplayName = displayName,
			Weight = 0.25f,
			Element = element,
			OutlineColor = color,
			Modifiers =
			{
				new ModData( resistStat, 50f, ModifierType.Flat ),
				new ModData( physicalAsStat, 50f, ModifierType.Flat )
			}
		};
	}
}
